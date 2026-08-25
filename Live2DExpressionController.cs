using System;
using UnityEngine;
using Live2D.Cubism.Framework.Expression;
using Live2D.Cubism.Framework;


public enum Exp3s
{
    normal,
    smile
}

public static class Exp3sHelper
{
    // NOTE: falseを返す表情は目パチを無効化する（目を閉じる表情など）
    public static bool EnablesEyeBlink(Exp3s exp) => exp switch
    {
        Exp3s.smile => false,
        _ => true
    };
}

public class Live2DExpressionController : MonoBehaviour
{
    private CubismExpressionController cubismExpressionController;
	private CubismEyeBlinkController cubismEyeBlinkController;
	private CubismAutoEyeBlinkInput cubismAutoEyeBlinkInput;

	[Serializable]
	private struct ExpressionMapping
	{
		public Exp3s Expression;
		public CubismExpressionData Asset;
	}

	// NOTE: 2026/08/15 ExpressionsList内の.exp3アセット名はモデル制作時の任意の名前（例: "smile.exp3"）であり、
	// Exp3sのメンバー名と一致する保証がない。そのため名前や並び順ではなく、Inspectorで明示的に
	// 紐付けたアセット参照で検索する（並び替えやリネームをしても壊れない）。
	[SerializeField] private ExpressionMapping[] expressionMappings;

	private void Awake()
	{
		// NOTE: チュートリアル動画用にInspectorでのアタッチではなく、同一GameObjectへのアタッチをGetComponentで自動取得する。
		// 取得できない場合（コンポーネント未アタッチ）は失敗を許容し、以降のnullチェックで検知する。
		cubismExpressionController = GetComponent<CubismExpressionController>();
		cubismEyeBlinkController = GetComponent<CubismEyeBlinkController>();
		cubismAutoEyeBlinkInput = GetComponent<CubismAutoEyeBlinkInput>();

		if (cubismExpressionController == null)
		{
			Debug.LogError($"{name}: {nameof(CubismExpressionController)}が同一GameObjectにアタッチされていません。", this);
		}

		foreach (Exp3s exp in Enum.GetValues(typeof(Exp3s)))
		{
			if (!TryGetMappedAsset(exp, out _))
			{
				Debug.LogError($"{name}: {nameof(expressionMappings)}に\"{exp}\"に対応するCubismExpressionDataが設定されていません。Inspectorで設定してください。", this);
			}
		}

		if (cubismEyeBlinkController == null)
		{
			Debug.LogWarning($"{name}: {nameof(CubismEyeBlinkController)}が同一GameObjectにアタッチされていません。表情変更時のまばたき制御は行われません。", this);
		}

		if (cubismAutoEyeBlinkInput == null)
		{
			Debug.LogWarning($"{name}: {nameof(CubismAutoEyeBlinkInput)}が同一GameObjectにアタッチされていません。表情変更時のまばたき制御は行われません。", this);
		}
	}

	public void OnPlayExpressionButtonClick()
	{
		ChangeExpressionWithExp3(Exp3s.smile);
	}

	public void OnResetExpressionButtonClick()
	{
		ChangeExpressionWithExp3(Exp3s.normal);
	}

    public void ChangeExpressionon(string expression)
	{
		if (!Enum.TryParse<Exp3s>(expression, ignoreCase: true, out var exp))
		{
			Debug.LogWarning($"{name}: 未知の表情名\"{expression}\"が指定されたため、{nameof(Exp3s.normal)}にフォールバックします。呼び出し元の表情名が{nameof(Exp3s)}のメンバー名({string.Join(", ", Enum.GetNames(typeof(Exp3s)))})と一致しているか確認してください。", this);
			exp = Exp3s.normal;
		}

		ChangeExpressionWithExp3(exp);
	}

	private void ChangeExpressionWithExp3(Exp3s exp)
	{
		if (cubismExpressionController == null)
		{
			Debug.LogError($"{name}: {nameof(cubismExpressionController)}が未設定のため表情を変更できません。{nameof(CubismExpressionController)}が同一GameObjectにアタッチされているか確認してください。", this);
			return;
		}

		var index = FindExpressionIndex(exp);
		if (index < 0)
		{
			Debug.LogError($"{name}: \"{exp}\"に対応する表情が見つかりません。{nameof(expressionMappings)}の設定、またはExpressionsListへの登録を確認してください。", this);
			return;
		}

		SetEyeBlinkEnabled(Exp3sHelper.EnablesEyeBlink(exp));
		cubismExpressionController.CurrentExpressionIndex = index;
	}

	// NOTE: 2026/08/15 CurrentExpressionIndexにenumの値をそのままキャストすると、Inspector上でExpressionsListの
	// 並び順を入れ替えられただけで無警告のまま違う表情が適用されてしまう。
	// そのためInspectorで明示的に紐付けたアセット参照(expressionMappings)を使い、
	// そのアセットがExpressionsList内で実際に何番目にあるかをその都度検索して求める。
	private int FindExpressionIndex(Exp3s exp)
	{
		if (!TryGetMappedAsset(exp, out var targetAsset))
		{
			return -1;
		}

		var expressionsList = cubismExpressionController.ExpressionsList;
		var objects = expressionsList != null ? expressionsList.CubismExpressionObjects : null;
		if (objects == null)
		{
			return -1;
		}

		for (var i = 0; i < objects.Length; i++)
		{
			if (objects[i] == targetAsset)
			{
				return i;
			}
		}

		return -1;
	}

	private bool TryGetMappedAsset(Exp3s exp, out CubismExpressionData asset)
	{
		if (expressionMappings != null)
		{
			foreach (var mapping in expressionMappings)
			{
				if (mapping.Expression == exp && mapping.Asset != null)
				{
					asset = mapping.Asset;
					return true;
				}
			}
		}

		asset = null;
		return false;
	}

	// NOTE: CubismEyeBlinkControllerはCubismExpressionControllerより後に
	// 毎フレーム実行され、BlendMode=OverrideでEyeOpeningの値をそのまま上書きする。そのためenabled=trueの
	// まま残すと、表情(exp3)側が設定した閉じ目の値を毎フレーム強制的に上書きしてしまい、目を閉じる
	// 表情が機能しなくなる。無効化時はコントローラー自体を止め、表情側の値をそのまま反映させる。
	// 無効化した瞬間がまばたきの途中と重なっても、直後にExpressionController側の値で上書きされるため
	// 半開きで固定されることはない。
	private void SetEyeBlinkEnabled(bool enable)
	{
		if (cubismAutoEyeBlinkInput != null)
		{
			cubismAutoEyeBlinkInput.enabled = enable;
			if (enable)
			{
				cubismAutoEyeBlinkInput.Reset();
			}
		}

		if (cubismEyeBlinkController != null)
		{
			cubismEyeBlinkController.enabled = enable;
		}
	}
}
