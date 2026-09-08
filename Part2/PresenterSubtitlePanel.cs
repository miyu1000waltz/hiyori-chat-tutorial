using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// LLM応答テキスト・ASR認識テキストをViewSubtitlePanelへ橋渡しするPresenter。
/// UniRxは使わず、LLMController/AsrApiControllerの素のC# eventを購読する。
/// </summary>
public class PresenterSubtitlePanel : MonoBehaviour
{
    [SerializeField] private LLMController    llmController;
    [FormerlySerializedAs("asrController")]
    [SerializeField] private AsrApiController    asrApiController;
    [SerializeField] private ViewSubtitlePanel view;

    void Awake()
    {
        if (llmController == null)
        {
            Debug.LogError($"{name}: Inspectorで{nameof(llmController)}(LLMController)を設定してください。", this);
        }

        if (asrApiController == null)
        {
            Debug.LogError($"{name}: Inspectorで{nameof(asrApiController)}(AsrApiController)を設定してください。", this);
        }

        if (view == null)
        {
            Debug.LogError($"{name}: Inspectorで{nameof(view)}(ViewSubtitlePanel)を設定してください。", this);
        }
    }

    void Start()
    {
        if (view == null) return;

        if (llmController != null)
            llmController.OnResponseText += view.Show;

        if (asrApiController != null)
            asrApiController.OnRecognizedText += view.Show;
    }

    void OnDestroy()
    {
        if (view == null) return;

        if (llmController != null)
            llmController.OnResponseText -= view.Show;

        if (asrApiController != null)
            asrApiController.OnRecognizedText -= view.Show;
    }
}
