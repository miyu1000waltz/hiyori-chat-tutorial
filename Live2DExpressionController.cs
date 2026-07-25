using UnityEngine;
using Live2D.Cubism.Framework.Expression;

public enum Exp3s
{
    normal,
    bother
}


struct Expression
{
	public const string normal = "normal";
	public const string bother = "bother";
}


public class Live2DExpressionController : MonoBehaviour
{
    [SerializeField] private CubismExpressionController cubismExpressionController;
	public void OnButtonClick()
	{
		ChangeExpressionon(expression: Expression.bother);
	}

	public void OnButtonClick2()
	{
		ChangeExpressionon(expression: Expression.normal);
	}

    public void ChangeExpressionon(string expression)
	{
		switch (expression)
		{
            case Expression.normal:
				ChangeExpressionWithExp3(Exp3s.normal);
				return;
			case Expression.bother:
				ChangeExpressionWithExp3(Exp3s.bother);
				return;
			default:
				ChangeExpressionWithExp3(Exp3s.normal);
				return;
		}
	}

	private void ChangeExpressionWithExp3(Exp3s exp)
	{
		cubismExpressionController.CurrentExpressionIndex = (int)exp;
		return;
	}
}
