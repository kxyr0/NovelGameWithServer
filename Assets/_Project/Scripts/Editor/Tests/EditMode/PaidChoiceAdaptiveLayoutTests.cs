using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class PaidChoiceAdaptiveLayoutTests
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/_MyProject/Prefabs/UserInterface/Story/ChoiceButtonPrefabPPPaid.prefab",
        "Assets/_MyProject/Prefabs/UserInterface/Story/ChoiceButtonPrefabPPPaid 1.prefab"
    };

    [Test]
    public void PaidChoicePrefabs_UseSlicedBackgroundAndExactCostHierarchy()
    {
        foreach (string path in PrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            Button button = prefab.GetComponentInChildren<Button>(true);
            Assert.That(button, Is.Not.Null, path);
            Assert.That(button.GetComponent<Image>()?.type, Is.EqualTo(Image.Type.Sliced), path);

            RectTransform buttonRect = button.transform as RectTransform;
            Assert.That(buttonRect, Is.Not.Null, path);

            Transform cost = button.transform.Find("Cost");
            Assert.That(cost, Is.Not.Null, $"Required direct child Button/Cost is missing: {path}");

            PaidChoiceAdaptiveLayout[] layouts = prefab.GetComponentsInChildren<PaidChoiceAdaptiveLayout>(true);
            Assert.That(layouts, Has.Length.EqualTo(1), path);
            Assert.That(layouts[0].transform, Is.SameAs(cost), $"Layout must be placed only on Cost: {path}");

            TMP_Text costText = cost.Find("CostText")?.GetComponent<TMP_Text>();
            RectTransform image = cost.Find("Image") as RectTransform;
            Assert.That(costText, Is.Not.Null, $"Required direct child Cost/CostText is missing: {path}");
            Assert.That(image, Is.Not.Null, $"Required direct child Cost/Image is missing: {path}");

            PriceIconPreferredWidthSpacing spacing = costText.GetComponent<PriceIconPreferredWidthSpacing>();
            Assert.That(spacing, Is.Not.Null, path);
            Assert.That(spacing.CostText, Is.SameAs(costText), path);
            Assert.That(spacing.IconRect, Is.SameAs(image), path);

            RectTransform costRect = cost as RectTransform;
            Assert.That(costRect.anchorMin.x, Is.EqualTo(1f).Within(0.001f), path);
            Assert.That(costRect.anchorMax.x, Is.EqualTo(1f).Within(0.001f), path);

            Transform bodyText = FindText(prefab.transform, "BodyText")?.transform;
            Assert.That(bodyText, Is.Not.Null, path);
            Assert.That(bodyText.IsChildOf(cost), Is.False, $"BodyText must never belong to Cost: {path}");
        }
    }

    [Test]
    public void PaidChoiceCost_MovesOneExactStepPerBodyTextLineAndReturnsOnShrink()
    {
        foreach (string path in PrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                RectTransform container = instance.transform.Find("Container") as RectTransform;
                Button button = instance.GetComponentInChildren<Button>(true);
                RectTransform buttonRect = button != null ? button.transform as RectTransform : null;
                PaidChoiceAdaptiveLayout layout = instance.GetComponentInChildren<PaidChoiceAdaptiveLayout>(true);
                RectTransform cost = layout != null ? layout.transform as RectTransform : null;

                Assert.That(container, Is.Not.Null, path);
                Assert.That(buttonRect, Is.Not.Null, path);
                Assert.That(layout, Is.Not.Null, path);
                Assert.That(cost, Is.Not.Null, path);

                container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500f);
                container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160f);
                TMP_Text bodyText = FindText(instance.transform, "BodyText");
                Assert.That(bodyText, Is.Not.Null, path);
                bodyText.text = string.Empty;
                bodyText.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                layout.CaptureCurrentPositionAsCenter();

                float referenceHeight = buttonRect.rect.height;
                const float downwardOffset = 25f;
                layout.SetHeightTracking(referenceHeight, downwardOffset);

                Vector2 costSizeBefore = cost.rect.size;
                Vector2 anchoredPositionBefore = cost.anchoredPosition;

                bodyText.text = "One line";
                bodyText.ForceMeshUpdate(true, true);
                container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 320f);
                Canvas.ForceUpdateCanvases();
                layout.RefreshNow();

                AssertVector2Within(cost.rect.size, costSizeBefore, 0.01f,
                    path + " Cost must not stretch with the choice body");
                Assert.That(cost.anchoredPosition.x,
                    Is.EqualTo(anchoredPositionBefore.x).Within(0.01f),
                    path + " Cost X must never change");
                Assert.That(cost.anchoredPosition.y,
                    Is.EqualTo(anchoredPositionBefore.y - downwardOffset).Within(0.01f),
                    path + " One BodyText line must move Cost down by exactly one step");

                bodyText.text = "Line one\nLine two";
                bodyText.ForceMeshUpdate(true, true);
                container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 480f);
                Canvas.ForceUpdateCanvases();
                layout.RefreshNow();

                Assert.That(cost.anchoredPosition.y,
                    Is.EqualTo(anchoredPositionBefore.y - downwardOffset * 2f).Within(0.01f),
                    path + " Two BodyText lines must move Cost down by exactly two steps");

                bodyText.text = "One line again";
                bodyText.ForceMeshUpdate(true, true);
                container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 320f);
                Canvas.ForceUpdateCanvases();
                layout.SendMessage("OnRectTransformDimensionsChange", SendMessageOptions.DontRequireReceiver);
                layout.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

                Assert.That(cost.anchoredPosition.y,
                    Is.EqualTo(anchoredPositionBefore.y - downwardOffset).Within(0.01f),
                    path + " Returning from two lines to one must remove exactly one step");

                bodyText.text = string.Empty;
                bodyText.ForceMeshUpdate(true, true);
                container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160f);
                Canvas.ForceUpdateCanvases();
                layout.SendMessage("OnRectTransformDimensionsChange", SendMessageOptions.DontRequireReceiver);
                layout.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

                AssertVector2Within(cost.anchoredPosition, anchoredPositionBefore, 0.01f,
                    path + " Empty BodyText must automatically restore the exact center");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    private static void AssertVector2Within(Vector2 actual, Vector2 expected, float tolerance, string message)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), message);
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), message);
    }

    private static TMP_Text FindText(Transform root, string objectName)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == objectName)
                return texts[i];
        }

        return null;
    }
}
