using UnityEngine;

// Test room for the flat (non-VR) rig: floor, table, a few grabbable props and a button that reacts to USE.
// Used until the real game scenes are migrated; then FlatRig replaces the VR camera rig there.
public class FlatBoot : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 60;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 2;
        Box("Table", new Vector3(0, 0.4f, 1.6f), new Vector3(1.6f, 0.8f, 0.8f), new Color(0.45f, 0.3f, 0.2f), false);
        Box("Wall", new Vector3(0, 1.5f, 4), new Vector3(8, 3, 0.2f), new Color(0.3f, 0.6f, 0.45f), false);
        var colors = new[] { Color.red, Color.yellow, Color.cyan, Color.magenta };
        for (int i = 0; i < 4; i++) Box("Prop" + i, new Vector3(-0.55f + i * 0.36f, 0.9f, 1.5f), Vector3.one * 0.14f, colors[i], true);
        var b = Box("UseMe", new Vector3(0.6f, 0.83f, 1.85f), new Vector3(0.18f, 0.06f, 0.18f), Color.green, false);
        b.AddComponent<UseButton>();
        var l = new GameObject("Sun").AddComponent<Light>(); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(50, -30, 0);
        FlatRig.Create(Vector3.zero);
    }

    static GameObject Box(string n, Vector3 p, Vector3 s, Color c, bool dyn)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
        g.transform.position = p; g.transform.localScale = s;
        g.GetComponent<Renderer>().material.color = c;
        if (dyn) g.AddComponent<Rigidbody>().mass = 0.3f;
        return g;
    }
}

public class UseButton : MonoBehaviour
{
    void OnUse() { var r = GetComponent<Renderer>(); r.material.color = r.material.color == Color.green ? Color.red : Color.green; }
}
