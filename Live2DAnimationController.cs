using UnityEngine;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework;

struct Animations
{
    // NOTE: 2024/03/14 archaicケースを追加した。
    public const string reset = "RESET";
    public const string surprised = "surprised";
}

public class Live2DAnimationController : MonoBehaviour
{
    [SerializeField] private CubismMotionController _motionController;
    [SerializeField] private AnimationClip[] animSurpized;
    public GameObject model;

    public void OnButtonClick()
	{
		ChangeAnimation(animation: Animations.surprised);
	}

    public void OnButtonClick2()
	{
		CubismAnimationRelease();
	}

    public void ChangeAnimation(string animation)
    {
        switch (animation)
		{
            case Animations.reset:
                CubismAnimationRelease();
				return;
            case Animations.surprised:
                CubismAnimationPlayer(animSurpized);
                return;
			default:
				CubismAnimationRelease();
				return;
		}
    }

    public void CubismAnimationPlayer(AnimationClip[] anim, bool isRoop =false)
    {
        model.GetComponent<CubismEyeBlinkController>().enabled = false;
        _motionController.PlayAnimation(anim[0], layerIndex: 0, isLoop: false, speed: 1.0f);
    }

    public void CubismAnimationRelease()
    {
        model.GetComponent<CubismEyeBlinkController>().enabled = true;
        _motionController.StopAnimation(1, 0);
    }


}
