using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SpeechController : MonoBehaviour
{
    public AudioSource[] audioSource;
    public AudioClip audioclip;

    [SerializeField] private string ttsServerUrl = "http://localhost:8100/synthesize";

    [Serializable]
    private class SynthesizeRequest
    {
        public string msg;
    }

    void Awake()
    {
        audioSource = gameObject.GetComponents<AudioSource>();
        Debug.Log($"ttsServerUrl {ttsServerUrl}");
    }

    public void OnButtonClicked()
    {
        PlayAudioClip();
    }

    public void OnButtonClicked2()
    {
        TextToSpeech("こんにちは！百瀬ひよりです。");
    }

    public void PlayAudioClip()
    {
        audioSource[0].clip = audioclip;
        audioSource[0].Play();
    }

    public void TextToSpeech(string text, System.Action onComplete = null)
    {
        StartCoroutine(TextToSpeechCoroutine(text, onComplete));
    }

    private IEnumerator TextToSpeechCoroutine(string text, System.Action onComplete)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SynthesizeRequest { msg = text }));

        using (UnityWebRequest www = new UnityWebRequest(ttsServerUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SpeechController] TTS request failed: {www.error} (HTTP {www.responseCode})");
                onComplete?.Invoke();
                yield break;
            }

            AudioClip clip = WavToAudioClip(www.downloadHandler.data);
            if (clip == null)
            {
                onComplete?.Invoke();
                yield break;
            }


            audioSource[0].clip = clip;
            audioSource[0].Play();

            yield return new WaitForSeconds(clip.length);
            onComplete?.Invoke();
        }
    }

    private AudioClip WavToAudioClip(byte[] wav)
    {
        // WAV最小ヘッダサイズ(44byte)未満は不正データとして弾く
        if (wav == null || wav.Length < 44)
        {
            Debug.LogError("[SpeechController] WAV data too short or null");
            return null;
        }

        // WAV仕様で決まった固定オフセットからメタデータを読み出す
        int channels = BitConverter.ToInt16(wav, 22);   // 22: NumChannels
        int sampleRate = BitConverter.ToInt32(wav, 24); // 24: SampleRate
        int bitDepth = BitConverter.ToInt16(wav, 34);   // 34: BitsPerSample

        // "data" チャンクを線形探索する（fmt の後に LIST 等が挟まる場合があるため固定オフセットでは取れない）
        int dataOffset = 12; // RIFFヘッダ(12byte)の直後からチャンク探索を開始
        while (dataOffset + 8 < wav.Length)
        {
            if (wav[dataOffset] == 'd' && wav[dataOffset + 1] == 'a' &&
                wav[dataOffset + 2] == 't' && wav[dataOffset + 3] == 'a')
            {
                dataOffset += 8; // チャンク名(4byte) + サイズフィールド(4byte)を読み飛ばして音声データ先頭へ
                break;
            }
            int chunkSize = BitConverter.ToInt32(wav, dataOffset + 4);
            dataOffset += 8 + chunkSize; // 次のチャンク先頭へ進む
        }

        // PCM整数値をUnityAudioClipが要求する -1.0〜1.0 のfloatに正規化する
        int bytesPerSample = bitDepth / 8;
        int sampleCount = (wav.Length - dataOffset) / bytesPerSample;
        float[] audioData = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int byteIndex = dataOffset + i * bytesPerSample;
            if (bitDepth == 16)
            {
                audioData[i] = BitConverter.ToInt16(wav, byteIndex) / 32768.0f; // 16bit: -32768〜32767 → -1.0〜1.0
            }
            else if (bitDepth == 8)
            {
                audioData[i] = (wav[byteIndex] - 128) / 128.0f; // 8bit: 0〜255(符号なし) を -1.0〜1.0 にシフト＆正規化
            }
        }

        // ステレオの場合、総サンプル数をチャンネル数で割ったものがAudioClipの長さになる
        int samplesPerChannel = sampleCount / channels;
        AudioClip clip = AudioClip.Create("tts_voice", samplesPerChannel, channels, sampleRate, false);
        clip.SetData(audioData, 0);
        return clip;
    }
}
