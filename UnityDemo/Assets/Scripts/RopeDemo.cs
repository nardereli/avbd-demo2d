using UnityEngine;
using UnityEngine.UI;

public class RopeDemo : MonoBehaviour
{
    public bool spawnSecondRope = false;
    private GameObject segmentPrefab;

    void Awake()
    {
        segmentPrefab = Resources.Load<GameObject>("RopeSegment");
    }

    void Start()
    {
        var startA = CreateAnchor(new Vector3(-2f, 0f, 0f));
        var endA = CreateAnchor(new Vector3(2f, 0f, 0f));
        var ropeA = new GameObject("RopeA").AddComponent<RopeController>();
        ropeA.segmentPrefab = segmentPrefab;
        ropeA.startPoint = startA;
        ropeA.endPoint = endA;

        if (spawnSecondRope)
        {
            var startB = CreateAnchor(new Vector3(-1f, 1f, 0f));
            var endB = CreateAnchor(new Vector3(1f, 1f, 0f));
            var ropeB = new GameObject("RopeB").AddComponent<RopeController>();
            ropeB.segmentPrefab = segmentPrefab;
            ropeB.startPoint = startB;
            ropeB.endPoint = endB;
        }

        BuildUI(ropeA);
    }

    Transform CreateAnchor(Vector3 position)
    {
        GameObject go = new GameObject("Anchor");
        go.transform.position = position;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        return go.transform;
    }

    void BuildUI(RopeController rope)
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var toggleGO = new GameObject("AutoLengthToggle", typeof(Toggle), typeof(RectTransform));
        toggleGO.transform.SetParent(canvasGO.transform, false);
        var toggle = toggleGO.GetComponent<Toggle>();
        toggle.isOn = rope.autoLength;
        toggle.onValueChanged.AddListener(rope.SetAutoLength);

        var sliderGO = new GameObject("StretchSlider", typeof(Slider), typeof(RectTransform));
        sliderGO.transform.SetParent(canvasGO.transform, false);
        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0.1f;
        slider.maxValue = 5f;
        slider.value = rope.stretchLimit;
        slider.onValueChanged.AddListener(rope.SetStretchLimit);
    }
}

