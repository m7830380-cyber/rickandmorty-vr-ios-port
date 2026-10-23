using System.Collections.Generic;
using UnityEngine;

// Flat-screen replacement for the VR rig: no headset, no motion controllers.
// Phone controls (landscape):
//   left stick            walk / strafe
//   drag on right half    look around
//   right stick           move the ACTIVE hand up/down/left/right
//   REACH+ / REACH-       push the active hand forward / pull it back
//   L / R                 choose which hand the right stick drives
//   GRAB                  grab / release with the active hand
//   USE                   "trigger" on the held object (or poke what the hand touches)
//   THROW                 fling the held object forward
//   RESET                 hands back to rest
// Desktop/editor: WASD walk, right mouse look, arrow keys hand, Q/E reach, Tab hand, F grab, G use, T throw.
public class FlatRig : MonoBehaviour
{
    public float walkSpeed = 2.2f, lookSpeed = 0.18f, handSpeed = 0.9f, reachSpeed = 0.7f, grabRadius = 0.18f;

    CharacterController body;
    Transform head, cam;
    readonly Hand[] hands = new Hand[2];
    int active = 1;
    float yaw, pitch, vy;

    class Hand
    {
        public Transform t; public Vector3 rest, local; public Rigidbody held; public Vector3 lastPos, vel; public Renderer r;
    }

    // touch state
    class Stick { public Rect area; public int finger = -1; public Vector2 origin, value; }
    Stick move = new Stick(), arm = new Stick();
    int lookFinger = -1; Vector2 lookLast;
    readonly Dictionary<string, Rect> buttons = new Dictionary<string, Rect>();
    readonly HashSet<string> held = new HashSet<string>(), pressed = new HashSet<string>();
    Texture2D white;
    GUIStyle label;

    public static FlatRig Create(Vector3 pos)
    {
        var go = new GameObject("FlatRig");
        go.transform.position = pos;
        return go.AddComponent<FlatRig>();
    }

    void Awake()
    {
        body = gameObject.AddComponent<CharacterController>();
        body.height = 1.7f; body.radius = 0.25f; body.center = new Vector3(0, 0.85f, 0);
        head = new GameObject("Head").transform; head.SetParent(transform, false); head.localPosition = new Vector3(0, 1.6f, 0);
        var c = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
        c.tag = "MainCamera"; c.nearClipPlane = 0.03f; c.fieldOfView = 70;
        cam = c.transform; cam.SetParent(head, false); cam.localPosition = Vector3.zero; cam.localRotation = Quaternion.identity;
        for (int i = 0; i < 2; i++)
        {
            var h = new Hand();
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = i == 0 ? "LeftHand" : "RightHand";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.09f;
            h.t = go.transform; h.t.SetParent(cam, false);
            h.rest = new Vector3(i == 0 ? -0.22f : 0.22f, -0.22f, 0.42f); h.local = h.rest;
            h.r = go.GetComponent<Renderer>();
            hands[i] = h;
        }
        white = Texture2D.whiteTexture;
        Input.multiTouchEnabled = true;
    }

    void Update()
    {
        Layout();
        ReadTouches();
        ReadKeyboard();

        // look
        pitch = Mathf.Clamp(pitch, -80, 80);
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        head.localRotation = Quaternion.Euler(pitch, 0, 0);

        // walk
        Vector3 m = transform.TransformDirection(new Vector3(move.value.x, 0, move.value.y)) * walkSpeed;
        vy = body.isGrounded ? -1f : vy - 9.8f * Time.deltaTime;
        m.y = vy;
        body.Move(m * Time.deltaTime);

        // hands
        if (pressed.Contains("L")) active = 0;
        if (pressed.Contains("R")) active = 1;
        var h = hands[active];
        h.local += new Vector3(arm.value.x, arm.value.y, 0) * handSpeed * Time.deltaTime;
        if (held.Contains("REACH+")) h.local.z += reachSpeed * Time.deltaTime;
        if (held.Contains("REACH-")) h.local.z -= reachSpeed * Time.deltaTime;
        h.local = new Vector3(Mathf.Clamp(h.local.x, -0.6f, 0.6f), Mathf.Clamp(h.local.y, -0.6f, 0.45f), Mathf.Clamp(h.local.z, 0.2f, 0.85f));
        if (pressed.Contains("RESET")) foreach (var x in hands) x.local = x.rest;
        for (int i = 0; i < 2; i++)
        {
            var x = hands[i];
            x.t.localPosition = Vector3.Lerp(x.t.localPosition, x.local, 1 - Mathf.Exp(-20 * Time.deltaTime));
            x.vel = (x.t.position - x.lastPos) / Mathf.Max(Time.deltaTime, 1e-4f); x.lastPos = x.t.position;
            x.r.material.color = i == active ? new Color(1f, 0.85f, 0.6f) : new Color(0.7f, 0.6f, 0.5f);
            if (x.held != null) { x.held.MovePosition(x.t.position); x.held.MoveRotation(cam.rotation); }
        }

        if (pressed.Contains("GRAB")) { if (h.held != null) Release(h, h.vel); else Grab(h); }
        if (pressed.Contains("THROW") && h.held != null) Release(h, cam.forward * 7f + h.vel);
        if (pressed.Contains("USE"))
        {
            Component target = h.held;
            if (target == null) { var cs = Physics.OverlapSphere(h.t.position, grabRadius); if (cs.Length > 0) target = cs[0]; }
            if (target != null) target.SendMessageUpwards("OnUse", this, SendMessageOptions.DontRequireReceiver);
        }
        pressed.Clear();
    }

    void Grab(Hand h)
    {
        Rigidbody best = null; float bd = float.MaxValue;
        foreach (var c in Physics.OverlapSphere(h.t.position, grabRadius * 1.5f))
        {
            var rb = c.attachedRigidbody; if (rb == null || rb.isKinematic && rb.GetComponent<Grabbed>() == null) continue;
            float d = (c.ClosestPoint(h.t.position) - h.t.position).sqrMagnitude;
            if (d < bd) { bd = d; best = rb; }
        }
        if (best == null) return;
        foreach (var o in hands) if (o.held == best) o.held = null;
        best.gameObject.AddComponent<Grabbed>();
        best.isKinematic = true; h.held = best;
        best.SendMessage("OnGrab", this, SendMessageOptions.DontRequireReceiver);
    }

    void Release(Hand h, Vector3 v)
    {
        var rb = h.held; h.held = null;
        Destroy(rb.GetComponent<Grabbed>());
        rb.isKinematic = false; rb.velocity = v;
        rb.SendMessage("OnRelease", this, SendMessageOptions.DontRequireReceiver);
    }

    class Grabbed : MonoBehaviour { }

    // ---------------- input ----------------
    void Layout()
    {
        float u = Mathf.Min(Screen.width, Screen.height) / 7f, W = Screen.width, H = Screen.height;
        move.area = new Rect(0, H * 0.35f, W * 0.4f, H * 0.65f);
        arm.area = new Rect(W - 3.2f * u, H - 3.2f * u, 3.2f * u, 3.2f * u);
        buttons["GRAB"] = new Rect(W - 4.6f * u, H - 1.3f * u, 1.3f * u, 1.1f * u);
        buttons["USE"] = new Rect(W - 4.6f * u, H - 2.5f * u, 1.3f * u, 1.1f * u);
        buttons["THROW"] = new Rect(W - 4.6f * u, H - 3.7f * u, 1.3f * u, 1.1f * u);
        buttons["REACH+"] = new Rect(W - 3.2f * u, H - 4.4f * u, 1.5f * u, 1.0f * u);
        buttons["REACH-"] = new Rect(W - 1.6f * u, H - 4.4f * u, 1.5f * u, 1.0f * u);
        buttons["L"] = new Rect(W - 3.2f * u, 0.2f * u, 1.0f * u, 1.0f * u);
        buttons["R"] = new Rect(W - 2.1f * u, 0.2f * u, 1.0f * u, 1.0f * u);
        buttons["RESET"] = new Rect(W - 1.0f * u - 0.1f * u, 0.2f * u, 1.0f * u, 1.0f * u);
    }

    static Vector2 Gui(Vector2 p) { return new Vector2(p.x, Screen.height - p.y); }

    void ReadTouches()
    {
        var down = new HashSet<string>();
        float rad = Mathf.Min(Screen.width, Screen.height) / 9f;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i); var p = Gui(t.position);
            bool began = t.phase == TouchPhase.Began, ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            string hit = null;
            foreach (var b in buttons) if (b.Value.Contains(p)) hit = b.Key;
            if (hit != null && t.fingerId != move.finger && t.fingerId != arm.finger && t.fingerId != lookFinger)
            {
                if (!ended) down.Add(hit);
                if (began) pressed.Add(hit);
                continue;
            }
            foreach (var s in new[] { move, arm })
            {
                if (began && s.finger < 0 && s.area.Contains(p)) { s.finger = t.fingerId; s.origin = p; }
                if (s.finger == t.fingerId)
                {
                    var d = (p - s.origin) / rad; d.y = -d.y;
                    s.value = Vector2.ClampMagnitude(d, 1f);
                    if (ended) { s.finger = -1; s.value = Vector2.zero; }
                }
            }
            if (t.fingerId == move.finger || t.fingerId == arm.finger) continue;
            if (began && lookFinger < 0 && p.x > Screen.width * 0.4f) { lookFinger = t.fingerId; lookLast = p; }
            if (t.fingerId == lookFinger)
            {
                var d = p - lookLast; lookLast = p;
                yaw += d.x * lookSpeed; pitch += d.y * lookSpeed;
                if (ended) lookFinger = -1;
            }
        }
        held.Clear(); foreach (var d in down) held.Add(d);
    }

    void ReadKeyboard()
    {
        if (Input.touchCount > 0) return;
        if (move.finger < 0)
            move.value = Vector2.ClampMagnitude(new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)), 1);
        arm.value = new Vector2((Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
            (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
        if (Input.GetMouseButton(1)) { yaw += Input.GetAxis("Mouse X") * 3; pitch -= Input.GetAxis("Mouse Y") * 3; }
        if (Input.GetKey(KeyCode.E)) held.Add("REACH+");
        if (Input.GetKey(KeyCode.Q)) held.Add("REACH-");
        if (Input.GetKeyDown(KeyCode.Tab)) pressed.Add(active == 0 ? "R" : "L");
        if (Input.GetKeyDown(KeyCode.F)) pressed.Add("GRAB");
        if (Input.GetKeyDown(KeyCode.G)) pressed.Add("USE");
        if (Input.GetKeyDown(KeyCode.T)) pressed.Add("THROW");
        if (Input.GetKeyDown(KeyCode.R)) pressed.Add("RESET");
    }

    // ---------------- drawing ----------------
    void OnGUI()
    {
        if (label == null) { label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; }
        label.fontSize = (int)(Mathf.Min(Screen.width, Screen.height) / 26f);
        float rad = Mathf.Min(Screen.width, Screen.height) / 9f;
        DrawStick(move, rad, "MOVE", new Vector2(move.area.x + 1.6f * rad, Screen.height - 1.6f * rad));
        DrawStick(arm, rad, active == 0 ? "LEFT ARM" : "RIGHT ARM", arm.area.center);
        foreach (var b in buttons)
        {
            bool on = held.Contains(b.Key) || (b.Key == "L" && active == 0) || (b.Key == "R" && active == 1);
            GUI.color = on ? new Color(1, 0.8f, 0.3f, 0.75f) : new Color(1, 1, 1, 0.28f);
            GUI.DrawTexture(Shrink(b.Value, 4), white);
            GUI.color = Color.white;
            string txt = b.Key == "GRAB" && hands[active].held != null ? "DROP" : b.Key;
            GUI.Label(b.Value, txt, label);
        }
    }

    void DrawStick(Stick s, float rad, string name, Vector2 idle)
    {
        Vector2 c = s.finger >= 0 ? s.origin : idle;
        GUI.color = new Color(1, 1, 1, 0.18f);
        GUI.DrawTexture(new Rect(c.x - rad, c.y - rad, 2 * rad, 2 * rad), white);
        var k = c + new Vector2(s.value.x, -s.value.y) * rad;
        GUI.color = new Color(1, 1, 1, 0.55f);
        GUI.DrawTexture(new Rect(k.x - rad * 0.4f, k.y - rad * 0.4f, rad * 0.8f, rad * 0.8f), white);
        GUI.color = Color.white;
        GUI.Label(new Rect(c.x - rad, c.y - rad * 1.6f, 2 * rad, rad * 0.5f), name, label);
    }

    static Rect Shrink(Rect r, float d) { return new Rect(r.x + d, r.y + d, r.width - 2 * d, r.height - 2 * d); }
}
