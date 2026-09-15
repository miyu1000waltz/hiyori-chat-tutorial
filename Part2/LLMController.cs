using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class LLMController : MonoBehaviour
{
    [SerializeField] private string apiKey;
    const string URL = "https://generativelanguage.googleapis.com/v1beta/models/"
                     + "gemini-3.5-flash-lite:generateContent";

    private SpeechController speechController;
    private Live2DExpressionController expressionController;

    // LLM応答テキストが届いたときに発火する。PresenterSubtitlePanelが購読して字幕表示に使う。
    public event System.Action<string> OnResponseText;

    // LLMに人格・出力フォーマットを指示するシステムプロンプト。
    // expressionの取りうる値はLive2DExpressionController.Exp3sのメンバー名と一致させる（大文字小文字は無視される）。
    const string SYSTEM_PROMPT =
        "あなたはユーザーと会話する、桃瀬ひよりです。フレンドリーかつ簡潔な日本語で応答してください。\n" +
        "必ず次のJSON形式のみで出力し、それ以外の文字（説明文やコードブロック記号）は一切含めないでください。\n" +
        "{\"text\": string(セリフ本文), \"expression\": string(\"normal\" または \"smile\" のいずれか)}";

    // Gemini リクエスト/レスポンス共通の JSON マッピング用クラス
    [System.Serializable] class Part      { public string text; }
    [System.Serializable] class Content   { public Part[] parts; }
    [System.Serializable] class Candidate { public Content content; }
    [System.Serializable] class Response  { public Candidate[] candidates; }

    // レスポンススキーマ定義用クラス（generationConfig.responseSchema）
    [System.Serializable] class SchemaProperty     { public string type; }
    [System.Serializable] class SchemaPropertyEnum { public string type; public string[] @enum; }
    [System.Serializable] class ResponseProperties { public SchemaProperty text; public SchemaPropertyEnum expression; }
    [System.Serializable] class ResponseSchema     { public string type; public ResponseProperties properties; public string[] required; }
    [System.Serializable] class GenerationConfig   { public string responseMimeType; public ResponseSchema responseSchema; }
    [System.Serializable] class RequestBody        { public Content system_instruction; public Content[] contents; public GenerationConfig generationConfig; }

    // LLMが返す構造化出力のデコード先
    [System.Serializable] class LLMOutput { public string text; public string expression; }

    // CallLLMCoroutineの結果。成功/失敗をSuccessで区別し、呼び出し側で表示処理を分岐できるようにする。
    public readonly struct LLMResult
    {
        public readonly bool Success;
        public readonly string Text;

        public LLMResult(bool success, string text)
        {
            Success = success;
            Text = text;
        }
    }

    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    // trueの間は送信不可。キャラクターが喋り終わる、またはエラーになるまでtrueのまま。
    private bool isSpeaking = false;

    // ASRなど外部からLLM稼働中かどうかを判定するための公開プロパティ。
    public bool IsBusy => isSpeaking;

    private void Awake()
    {
        // NOTE: Inspectorでのアタッチではなく、同一GameObjectへのアタッチをGetComponentで自動取得する。
        // 取得できない場合（コンポーネント未アタッチ）は失敗を許容し、以降のnullチェックで検知する。
        speechController = GetComponent<SpeechController>();
        expressionController = GetComponent<Live2DExpressionController>();

        if (speechController == null)
        {
            Debug.LogError($"{name}: {nameof(SpeechController)}が同一GameObjectにアタッチされていません。", this);
        }

        if (expressionController == null)
        {
            Debug.LogError($"{name}: {nameof(Live2DExpressionController)}が同一GameObjectにアタッチされていません。", this);
        }
    }

    public void OnSendButtonClicked()
    {
        if (isSpeaking) return;

        string userInput = inputField.text;
        inputField.text = "";

        SubmitUserText(userInput);
    }

    // テキスト送信元(ボタン入力・ASR認識結果など)を問わず共通のLLM呼び出し経路。
    public void SubmitUserText(string userInput)
    {
        if (isSpeaking) return;

        isSpeaking = true;
        if (sendButton != null) sendButton.interactable = false;

        // LLM呼び出し前に表情をnormalへリセットする。前回の表情（smileなど）が
        // 次の応答が返るまで残り続けるのを防ぐ。
        if (expressionController != null)
        {
            expressionController.ChangeExpressionon("normal");
        }

        StartCoroutine(CallLLMCoroutine(userInput, result =>
        {
            OnResponseText?.Invoke(result.Text);

            if (result.Success && speechController != null)
            {
                speechController.TextToSpeech(result.Text, EndSpeaking);
            }
            else
            {
                EndSpeaking();
            }
        }));
    }

    private void EndSpeaking()
    {
        isSpeaking = false;
        if (sendButton != null) sendButton.interactable = true;
    }


    public IEnumerator CallLLMCoroutine(string userMessage, System.Action<LLMResult> onResult)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            onResult(new LLMResult(false, "（何か入力してください）"));
            yield break;
        }

        var requestBody = new RequestBody
        {
            system_instruction = new Content { parts = new[] { new Part { text = SYSTEM_PROMPT } } },
            contents = new[] { new Content { parts = new[] { new Part { text = userMessage } } } },
            generationConfig = new GenerationConfig
            {
                responseMimeType = "application/json",
                responseSchema = new ResponseSchema
                {
                    type = "OBJECT",
                    properties = new ResponseProperties
                    {
                        text       = new SchemaProperty { type = "STRING" },
                        expression = new SchemaPropertyEnum { type = "STRING", @enum = new[] { "normal", "smile" } }
                    },
                    required = new[] { "text", "expression" }
                }
            }
        };
        string json = JsonUtility.ToJson(requestBody);

        using var req = new UnityWebRequest(URL + "?key=" + apiKey, "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("通信エラー: " + req.error + "\n" + req.downloadHandler.text);
            onResult(new LLMResult(false, "（通信エラーが発生しました: " + req.error + "）"));
            yield break;
        }

        var res = JsonUtility.FromJson<Response>(req.downloadHandler.text);

        string rawOutput;
        try
        {
            rawOutput = res.candidates[0].content.parts[0].text;
        }
        catch (System.Exception e)
        {
            Debug.LogError("LLMレスポンスの取得に失敗しました: " + e.Message + "\n" + req.downloadHandler.text);
            onResult(new LLMResult(false, "（LLMレスポンスの取得に失敗しました）"));
            yield break;
        }

        if (string.IsNullOrEmpty(rawOutput))
        {
            Debug.LogError("LLMレスポンスが空でした。");
            onResult(new LLMResult(false, "（LLMレスポンスが空でした）"));
            yield break;
        }

        LLMOutput output;
        try
        {
            output = JsonUtility.FromJson<LLMOutput>(rawOutput);
        }
        catch (System.Exception e)
        {
            Debug.LogError("LLM出力のJSONデコードに失敗しました: " + e.Message + "\n" + rawOutput);
            onResult(new LLMResult(false, "（LLM出力のJSONデコードに失敗しました）"));
            yield break;
        }

        if (output == null || string.IsNullOrEmpty(output.text))
        {
            Debug.LogError("LLM出力のtextが空でした。\n" + rawOutput);
            onResult(new LLMResult(false, "（LLM出力のtextが空でした）"));
            yield break;
        }

        // 改行を除去
        string text = output.text.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");

        if (expressionController != null && !string.IsNullOrEmpty(output.expression))
        {
            expressionController.ChangeExpressionon(output.expression);
        }

        Debug.Log($"LLM出力結果: {text}");

        onResult(new LLMResult(true, text));
    }
}
