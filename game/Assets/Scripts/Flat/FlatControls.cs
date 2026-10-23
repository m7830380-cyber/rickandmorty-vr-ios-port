using System.Collections.Generic;
using OwlchemyVR;
using UnityEngine;

// Touch controls for playing without a headset (landscape phone):
//   left side            floating stick: walk around the current teleport zone (like walking in room scale)
//   drag elsewhere       look around
//   tap something        the hand on that side of the screen reaches for it: grabs it if it can be picked up,
//                        otherwise pokes it (buttons, switches, characters)
//   swipe while holding  throw the held item in the swipe direction
//   USE L / USE R        squeeze / use what that hand holds (spray, eat, fire...)
//   DROP L / DROP R      let go
//   TELEPORT (hold)      aim with the crosshair at a teleport spot, release to jump there
//   CROUCH, HAND         crouch; HAND mode: drags move the right hand instead of the camera (for levers, drawers)
//   MENU                 the game's own menu button; the gear opens the flat settings
// The game's own rig, hands and interaction code do everything else; this only says where the head and hands
// are and which wand buttons are pressed.
[DefaultExecutionOrder(-500)]
public class FlatControls : MonoBehaviour
{
    const float ReachDistance = 2.3f;
    const float WalkSpeed = 1.6f;
    const float PlayAreaRadius = 3f;

    enum HandMode { Rest, Reach, Hold, Poke, Throw, Free }

    class Hand
    {
        public HandController controller;
        public Flat_TrackedInputController input;
        public HandMode mode;
        public float modeTime;
        public Vector3 worldTarget, throwDir, freeOffset;
        public Vector3 pos;
        public Quaternion rot = Quaternion.identity;
        public bool initialised;
    }

    class Button
    {
        public string label;
        public Rect rect;
        public int finger = -1;
        public bool held, toggle, on;
    }

    MasterHMDAndInputController rig;
    Camera cam;
    Transform head;
    readonly Hand[] hands = new Hand[2];
    float yaw, pitch;
    Vector3 headLocal;
    bool crouched;
    FlatSettings settings;

    // touch state
    int stickFinger = -1, lookFinger = -1, secondFinger = -1;
    Vector2 stickOrigin, stickValue, lookLast, lookStart, secondLast;
    float lookStartTime, lookStartYaw, lookStartPitch;
    bool lookMoved;
    readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    readonly List<Button> order = new List<Button>();
    bool settingsOpen, logOpen;
    Vector2 logScroll;
    string toast;
    float toastUntil;
    float fps = 60f;
    Rect fpsRect;

    public static FlatControls Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        settings = FlatSettings.Current;
        settings.Apply();
        Input.multiTouchEnabled = true;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        foreach (var name in new[] { "USE L", "DROP L", "USE R", "DROP R", "TELEPORT", "CROUCH", "HAND", "MENU", "SET" })
        {
            var b = new Button { label = name, toggle = name == "CROUCH" || name == "HAND" };
            buttons[name] = b;
            order.Add(b);
        }
    }

    void Start()
    {
        rig = GetComponent<MasterHMDAndInputController>();
        cam = rig.HMDCamera;
        head = cam.transform;
        while (head.parent != null && head.parent != rig.HeadOrigin) head = head.parent;
        foreach (var tracked in head.GetComponentsInChildren<SteamVR_TrackedObject>(true)) tracked.enabled = false;
        // full-screen effects (bloom etc.) use shaders that only exist as DirectX programs, and cost a lot on a phone
        foreach (var fx in cam.GetComponents<UnityStandardAssets.ImageEffects.PostEffectsBase>()) fx.enabled = false;
        headLocal = new Vector3(0f, settings.playerHeight, 0f);
        for (int i = 0; i < 2 && i < rig.Hands.Count; i++)
        {
            hands[i] = new Hand
            {
                controller = rig.Hands[i],
                input = rig.Hands[i].GetHardwareTrackedController() as Flat_TrackedInputController
            };
            if (hands[i].input == null) Debug.LogError("[RMVR] hand " + i + " has no Flat_TrackedInputController");
        }
        Debug.Log("[RMVR] FlatControls on head '" + head.name + "', camera '" + cam.name + "'");
    }

    void Update()
    {
        if (rig == null) return;
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 0.05f);
        cam.fieldOfView = settings.fieldOfView;
        Layout();
        ReadTouches();
        ReadKeyboard();
        UpdateHead();
        for (int i = 0; i < 2; i++) UpdateHand(i);
        for (int i = 0; i < 2; i++)
            if (hands[i] != null && hands[i].input != null) hands[i].input.Tick();
    }

    // ---------------------------------------------------------------- input

    float Unit
    {
        get { return Mathf.Max(44f, Screen.height * 0.1f) * settings.buttonScale; }
    }

    Rect Safe
    {
        get
        {
            Rect s = Screen.safeArea;
            return new Rect(s.x, Screen.height - s.yMax, s.width, s.height);
        }
    }

    void Layout()
    {
        float u = Unit, g = u * 0.15f;
        Rect s = Safe;
        float right = s.xMax - g, bottom = s.yMax - g;
        Place("USE R", right - u * 1.6f, bottom - u * 1.6f, u * 1.6f, u * 1.6f);
        Place("DROP R", right - u * 2.9f - g, bottom - u * 1.1f, u * 1.3f, u * 1.1f);
        Place("TELEPORT", right - u * 1.6f, bottom - u * 2.7f - g, u * 1.6f, u);
        Place("HAND", right - u * 1.6f, bottom - u * 3.7f - g * 2, u * 1.6f, u * 0.9f);
        Place("CROUCH", right - u * 1.6f, bottom - u * 4.6f - g * 3, u * 1.6f, u * 0.9f);
        Place("USE L", s.x + g, s.y + u * 0.9f, u * 1.4f, u * 1.2f);
        Place("DROP L", s.x + g, s.y + u * 2.2f + g, u * 1.4f, u);
        Place("MENU", s.center.x - u * 1.25f, s.y + g, u * 1.2f, u * 0.75f);
        Place("SET", s.center.x + u * 0.05f, s.y + g, u * 1.2f, u * 0.75f);
        fpsRect = new Rect(s.x + g, s.y + g, u * 1.8f, u * 0.6f);
    }

    void Place(string name, float x, float y, float w, float h)
    {
        buttons[name].rect = new Rect(x, y, w, h);
    }

    static Vector2 GuiPos(Vector2 screen)
    {
        return new Vector2(screen.x, Screen.height - screen.y);
    }

    Button ButtonAt(Vector2 gui)
    {
        if (settings.hideButtons) return buttons["SET"].rect.Contains(gui) ? buttons["SET"] : null;
        foreach (var b in order)
            if (b.rect.Contains(gui)) return b;
        return null;
    }

    void ReadTouches()
    {
        stickValue = Vector2.zero;
        for (int t = 0; t < Input.touchCount; t++)
        {
            Touch touch = Input.GetTouch(t);
            Vector2 gui = GuiPos(touch.position);
            int id = touch.fingerId;
            if (touch.phase == TouchPhase.Began)
            {
                if (settingsOpen || logOpen) continue;
                if (fpsRect.Contains(gui) && settings.showFps)
                {
                    logOpen = true;
                    continue;
                }
                Button b = ButtonAt(gui);
                if (b != null)
                {
                    b.finger = id;
                    if (b.toggle) b.on = !b.on;
                    else b.held = true;
                    OnButtonPressed(b);
                }
                else if (gui.x < Screen.width * 0.4f && gui.y > Screen.height * 0.3f && stickFinger < 0)
                {
                    stickFinger = id;
                    stickOrigin = touch.position;
                }
                else if (lookFinger < 0)
                {
                    lookFinger = id;
                    lookLast = lookStart = touch.position;
                    lookStartTime = Time.unscaledTime;
                    lookStartYaw = yaw;
                    lookStartPitch = pitch;
                    lookMoved = false;
                }
                else if (secondFinger < 0)
                {
                    secondFinger = id;
                    secondLast = touch.position;
                }
                continue;
            }

            bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            if (id == stickFinger)
            {
                Vector2 d = (touch.position - stickOrigin) / (Unit * 1.2f);
                stickValue = Vector2.ClampMagnitude(d, 1f);
                if (ended) stickFinger = -1;
            }
            else if (id == lookFinger)
            {
                Vector2 delta = touch.position - lookLast;
                lookLast = touch.position;
                if ((touch.position - lookStart).magnitude > Screen.height * 0.02f) lookMoved = true;
                if (buttons["HAND"].on) MoveFreeHand(delta, 0f);
                else Look(delta);
                if (ended)
                {
                    lookFinger = -1;
                    EndLookTouch(touch.position, touch.phase == TouchPhase.Canceled);
                }
            }
            else if (id == secondFinger)
            {
                Vector2 delta = touch.position - secondLast;
                secondLast = touch.position;
                if (buttons["HAND"].on) MoveFreeHand(Vector2.zero, delta.y);
                if (ended) secondFinger = -1;
            }
            else
            {
                foreach (var b in order)
                    if (b.finger == id && ended)
                    {
                        b.finger = -1;
                        b.held = false;
                    }
            }
        }
        // fingers can vanish without an Ended phase when the app loses focus
        if (Input.touchCount == 0)
        {
            stickFinger = lookFinger = secondFinger = -1;
            foreach (var b in order)
            {
                b.finger = -1;
                b.held = false;
            }
        }

        for (int i = 0; i < 2; i++)
        {
            Hand h = hands[i];
            if (h == null || h.input == null) continue;
            h.input.Set(Flat_TrackedInputController.Btn.Use, buttons[i == 0 ? "USE L" : "USE R"].held || KeyUse(i));
            h.input.Set(Flat_TrackedInputController.Btn.Teleport, i == 1 && (buttons["TELEPORT"].held || Input.GetKey(KeyCode.Space)));
            h.input.Set(Flat_TrackedInputController.Btn.Menu, i == 1 && (buttons["MENU"].held || Input.GetKey(KeyCode.M)));
        }
        crouched = buttons["CROUCH"].on;
    }

    void OnButtonPressed(Button b)
    {
        switch (b.label)
        {
            case "DROP L": Release(0); break;
            case "DROP R": Release(1); break;
            case "SET": settingsOpen = true; break;
            case "HAND":
                Hand h = hands[1];
                if (h != null && b.on)
                {
                    h.freeOffset = RestOffset(1) + new Vector3(0f, 0.08f, 0.1f);
                    if (h.mode != HandMode.Hold) SetMode(h, HandMode.Free);
                }
                else if (h != null && h.mode == HandMode.Free) SetMode(h, HandMode.Rest);
                break;
        }
    }

    void Look(Vector2 delta)
    {
        float k = 0.12f * settings.lookSensitivity * (1080f / Mathf.Max(Screen.height, 1));
        yaw += delta.x * k;
        pitch += (settings.invertLook ? delta.y : -delta.y) * k;
        pitch = Mathf.Clamp(pitch, -85f, 85f);
    }

    void EndLookTouch(Vector2 pos, bool canceled)
    {
        if (canceled) return;
        float dt = Time.unscaledTime - lookStartTime;
        Vector2 d = pos - lookStart;
        if (!lookMoved && dt < 0.35f)
        {
            Tap(pos);
            return;
        }
        // a quick flick while a hand holds something throws it (and undoes the look the flick caused)
        if (dt < 0.3f && d.magnitude > Screen.height * 0.12f && !buttons["HAND"].on)
        {
            int side = lookStart.x < Screen.width * 0.5f ? 0 : 1;
            int i = IsHolding(side) ? side : IsHolding(1 - side) ? 1 - side : -1;
            if (i >= 0)
            {
                yaw = lookStartYaw;
                pitch = lookStartPitch;
                Vector3 dir = cam.transform.TransformDirection(new Vector3(d.x / Screen.height, d.y / Screen.height + 0.35f, 1f)).normalized;
                Throw(i, dir, Mathf.Clamp(d.magnitude / Screen.height / dt, 1f, 6f));
            }
        }
    }

    void ReadKeyboard()
    {
        // desktop / editor testing: WASD walk, arrows look, Q/E grab left/right at the crosshair
        Vector2 k = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        if (k != Vector2.zero) stickValue = k;
        float lx = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        float ly = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        if (lx != 0 || ly != 0) Look(new Vector2(lx, ly) * 600f * Time.unscaledDeltaTime);
        if (Input.GetKeyDown(KeyCode.Q)) TapWithHand(0, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        if (Input.GetKeyDown(KeyCode.E)) TapWithHand(1, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }

    static bool KeyUse(int i)
    {
        return Input.GetKey(i == 0 ? KeyCode.Z : KeyCode.C);
    }

    // ---------------------------------------------------------------- head

    void UpdateHead()
    {
        Transform origin = rig.HeadOrigin;
        if (stickValue.sqrMagnitude > 0.0001f)
        {
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 localMove = yawRot * new Vector3(stickValue.x, 0f, stickValue.y) * WalkSpeed * Time.deltaTime;
            Vector3 worldMove = origin.TransformVector(localMove);
            worldMove = Collide(head.position, worldMove);
            Vector3 next = headLocal + origin.InverseTransformVector(worldMove);
            Vector2 flat = Vector2.ClampMagnitude(new Vector2(next.x, next.z), PlayAreaRadius);
            headLocal = new Vector3(flat.x, headLocal.y, flat.y);
        }
        float height = crouched ? settings.playerHeight * 0.6f : settings.playerHeight;
        headLocal.y = Mathf.MoveTowards(headLocal.y, height, 2.5f * Time.deltaTime);
        head.localPosition = headLocal;
        head.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        UnityEngine.VR.InputTracking.headPosition = headLocal;
        UnityEngine.VR.InputTracking.headRotation = head.localRotation;
    }

    // Keeps the head out of walls and furniture; slides along what it touches. Loose props are pushed through.
    Vector3 Collide(Vector3 from, Vector3 move)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            float dist = move.magnitude;
            if (dist < 1e-5f) return move;
            RaycastHit best = default(RaycastHit);
            bool found = false;
            foreach (var hit in Physics.SphereCastAll(from, 0.18f, move / dist, dist + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsOwn(hit.collider)) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                if (hit.distance <= 0f) continue;
                if (!found || hit.distance < best.distance)
                {
                    best = hit;
                    found = true;
                }
            }
            if (!found) return move;
            move = Vector3.ProjectOnPlane(move, best.normal);
            move.y = 0f;
        }
        return move;
    }

    bool IsOwn(Collider c)
    {
        return c.transform.IsChildOf(rig.transform) || c.transform.IsChildOf(head);
    }

    // ---------------------------------------------------------------- hands

    static Vector3 RestOffset(int i)
    {
        return new Vector3(i == 0 ? -0.2f : 0.2f, -0.26f, 0.32f);
    }

    static Vector3 HoldOffset(int i)
    {
        return new Vector3(i == 0 ? -0.17f : 0.17f, -0.17f, 0.42f);
    }

    void SetMode(Hand h, HandMode m)
    {
        h.mode = m;
        h.modeTime = 0f;
    }

    bool IsHolding(int i)
    {
        return hands[i] != null && hands[i].controller.InteractionHandController.IsGrabbableCurrInHand;
    }

    void Tap(Vector2 screenPos)
    {
        TapWithHand(screenPos.x < Screen.width * 0.5f ? 0 : 1, screenPos);
    }

    void TapWithHand(int i, Vector2 screenPos)
    {
        Hand h = hands[i];
        if (h == null) return;
        if (IsHolding(i))
        {
            // a tap with a full hand on something else uses the other hand when that one is free
            if (!IsHolding(1 - i) && hands[1 - i] != null) TapWithHand(1 - i, screenPos);
            else Toast("Hands full: DROP or swipe to throw");
            return;
        }
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;
        if (!RaycastWorld(ray, out hit))
        {
            Toast("Nothing there");
            return;
        }
        if (hit.distance > ReachDistance)
        {
            Toast("Too far: walk closer");
            return;
        }
        h.worldTarget = hit.point - ray.direction * 0.03f;
        h.throwDir = ray.direction;
        SetMode(h, HandMode.Reach);
    }

    bool RaycastWorld(Ray ray, out RaycastHit result)
    {
        result = default(RaycastHit);
        bool found = false;
        foreach (var hit in Physics.RaycastAll(ray, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsOwn(hit.collider)) continue;
            if (!found || hit.distance < result.distance)
            {
                result = hit;
                found = true;
            }
        }
        return found;
    }

    void Release(int i)
    {
        Hand h = hands[i];
        if (h == null) return;
        if (h.input != null) h.input.Set(Flat_TrackedInputController.Btn.Grab, false);
        SetMode(h, buttons["HAND"].on && i == 1 ? HandMode.Free : HandMode.Rest);
    }

    void Throw(int i, Vector3 dir, float speed)
    {
        Hand h = hands[i];
        h.throwDir = dir * speed;
        SetMode(h, HandMode.Throw);
    }

    void MoveFreeHand(Vector2 delta, float depth)
    {
        Hand h = hands[1];
        if (h == null) return;
        if (h.mode != HandMode.Free && h.mode != HandMode.Hold) SetMode(h, HandMode.Free);
        float k = 1.2f / Mathf.Max(Screen.height, 1);
        h.freeOffset += new Vector3(delta.x * k, delta.y * k, depth * k);
        h.freeOffset = new Vector3(Mathf.Clamp(h.freeOffset.x, -0.6f, 0.6f), Mathf.Clamp(h.freeOffset.y, -0.7f, 0.5f), Mathf.Clamp(h.freeOffset.z, 0.15f, 0.85f));
    }

    void UpdateHand(int i)
    {
        Hand h = hands[i];
        if (h == null || h.input == null) return;
        Transform ct = cam.transform;
        h.modeTime += Time.deltaTime;
        bool holding = IsHolding(i);
        bool free = buttons["HAND"].on && i == 1;
        Vector3 target;
        Quaternion targetRot = ct.rotation * Quaternion.Euler(25f, 0f, 0f);
        float follow = 18f;

        switch (h.mode)
        {
            case HandMode.Reach:
                target = h.worldTarget;
                targetRot = Quaternion.LookRotation(h.throwDir, ct.up);
                follow = 14f;
                if (Vector3.Distance(h.pos, target) < 0.04f || h.modeTime > 0.45f)
                {
                    h.input.Set(Flat_TrackedInputController.Btn.Grab, true);
                    if (h.modeTime > 0.05f && h.input.IsDown(Flat_TrackedInputController.Btn.Grab))
                    {
                        if (holding) SetMode(h, HandMode.Hold);
                        else if (h.modeTime > 0.6f)
                        {
                            // nothing to pick up there: it was a press, so push a little further, then let go
                            h.input.Set(Flat_TrackedInputController.Btn.Grab, false);
                            h.worldTarget += h.throwDir.normalized * 0.06f;
                            SetMode(h, HandMode.Poke);
                        }
                    }
                }
                break;
            case HandMode.Poke:
                target = h.worldTarget;
                targetRot = Quaternion.LookRotation(h.throwDir, ct.up);
                if (h.modeTime > 0.25f) SetMode(h, free ? HandMode.Free : HandMode.Rest);
                break;
            case HandMode.Hold:
                target = ct.TransformPoint(free ? h.freeOffset : HoldOffset(i));
                if (!holding && h.modeTime > 0.2f)
                {
                    // the game took the item away (eaten, broken joint, put in a slot)
                    h.input.Set(Flat_TrackedInputController.Btn.Grab, false);
                    SetMode(h, free ? HandMode.Free : HandMode.Rest);
                }
                break;
            case HandMode.Throw:
                target = h.pos + h.throwDir * Time.deltaTime;
                targetRot = h.rot;
                follow = 1000f;
                if (h.modeTime > 0.1f)
                {
                    h.input.Set(Flat_TrackedInputController.Btn.Grab, false);
                    SetMode(h, free ? HandMode.Free : HandMode.Rest);
                }
                break;
            case HandMode.Free:
                target = ct.TransformPoint(h.freeOffset);
                targetRot = ct.rotation;
                break;
            default:
                target = ct.TransformPoint(RestOffset(i));
                break;
        }

        // the teleport aim uses the right hand's pointing direction: point it where the crosshair is
        if (i == 1 && buttons["TELEPORT"].held || i == 1 && Input.GetKey(KeyCode.Space))
        {
            target = ct.TransformPoint(new Vector3(0.12f, -0.12f, 0.25f));
            RaycastHit aim;
            Vector3 at = RaycastWorld(new Ray(ct.position, ct.forward), out aim) ? aim.point : ct.position + ct.forward * 8f;
            targetRot = Quaternion.LookRotation(at - target, Vector3.up);
        }

        if (!h.initialised)
        {
            h.pos = target;
            h.rot = targetRot;
            h.initialised = true;
        }
        float a = 1f - Mathf.Exp(-follow * Time.deltaTime);
        h.pos = Vector3.Lerp(h.pos, target, a);
        h.rot = Quaternion.Slerp(h.rot, targetRot, a);
        Transform t = h.controller.TrackedTransform;
        t.position = h.pos;
        t.rotation = h.rot;
    }

    // ---------------------------------------------------------------- UI

    public void Toast(string message)
    {
        toast = message;
        toastUntil = Time.unscaledTime + 1.6f;
    }

    GUIStyle boxStyle, labelStyle, smallStyle;
    Texture2D white;

    void Styles()
    {
        if (boxStyle != null) return;
        white = Texture2D.whiteTexture;
        boxStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        boxStyle.normal.textColor = Color.white;
        labelStyle = new GUIStyle(boxStyle) { alignment = TextAnchor.MiddleLeft };
        smallStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
    }

    void OnGUI()
    {
        if (rig == null) return;
        Styles();
        float u = Unit;
        boxStyle.fontSize = Mathf.RoundToInt(u * 0.24f);
        labelStyle.fontSize = Mathf.RoundToInt(u * 0.22f);

        if (settings.showFps)
        {
            string text = Mathf.RoundToInt(fps) + " fps" + (FlatLog.ErrorCount > 0 ? "  ⚠" + FlatLog.ErrorCount : "");
            Fill(fpsRect, new Color(0f, 0f, 0f, 0.35f));
            GUI.Label(fpsRect, " " + text, labelStyle);
        }

        if (!settings.hideButtons)
        {
            foreach (var b in order)
            {
                bool lit = b.held || (b.toggle && b.on);
                Color c = lit ? new Color(0.45f, 0.95f, 0.35f, settings.buttonOpacity + 0.2f) : new Color(0.1f, 0.1f, 0.1f, settings.buttonOpacity);
                Fill(b.rect, c);
                GUI.Label(b.rect, b.label == "SET" ? "⚙" : b.label, boxStyle);
            }
            if (stickFinger >= 0)
            {
                Vector2 o = GuiPos(stickOrigin);
                float r = u * 1.2f;
                Fill(new Rect(o.x - r, o.y - r, r * 2, r * 2), new Color(1f, 1f, 1f, 0.12f));
                Vector2 k = o + new Vector2(stickValue.x, -stickValue.y) * r;
                Fill(new Rect(k.x - r * 0.35f, k.y - r * 0.35f, r * 0.7f, r * 0.7f), new Color(1f, 1f, 1f, 0.35f));
            }
        }
        else
        {
            Fill(buttons["SET"].rect, new Color(0f, 0f, 0f, 0.15f));
            GUI.Label(buttons["SET"].rect, "⚙", boxStyle);
        }

        // crosshair
        float cs = u * 0.08f;
        Fill(new Rect(Screen.width * 0.5f - cs * 0.5f, Screen.height * 0.5f - cs * 0.5f, cs, cs), new Color(1f, 1f, 1f, 0.6f));

        if (toast != null && Time.unscaledTime < toastUntil)
        {
            Rect r = new Rect(Screen.width * 0.5f - u * 3f, Screen.height * 0.72f, u * 6f, u * 0.7f);
            Fill(r, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(r, toast, boxStyle);
        }

        if (settingsOpen) DrawSettings(u);
        if (logOpen) DrawLog(u);
    }

    void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    void DrawSettings(float u)
    {
        GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.toggle.fontSize = Mathf.RoundToInt(u * 0.22f);
        Rect s = Safe;
        Rect panel = new Rect(s.center.x - u * 4.5f, s.y + u * 0.3f, u * 9f, s.height - u * 0.6f);
        Fill(panel, new Color(0f, 0f, 0f, 0.85f));
        GUILayout.BeginArea(new Rect(panel.x + u * 0.3f, panel.y + u * 0.2f, panel.width - u * 0.6f, panel.height - u * 0.4f));
        GUILayout.Label("Settings");
        settings.lookSensitivity = Slider("Look sensitivity", settings.lookSensitivity, 0.2f, 3f, u);
        settings.fieldOfView = Slider("Field of view", settings.fieldOfView, 50f, 100f, u);
        settings.playerHeight = Slider("Height (m)", settings.playerHeight, 1.1f, 1.9f, u);
        settings.buttonScale = Slider("Button size", settings.buttonScale, 0.6f, 1.6f, u);
        settings.buttonOpacity = Slider("Button opacity", settings.buttonOpacity, 0.1f, 0.9f, u);
        settings.invertLook = GUILayout.Toggle(settings.invertLook, " Invert look", GUILayout.Height(u * 0.6f));
        settings.hideButtons = GUILayout.Toggle(settings.hideButtons, " Hide buttons (gear stays)", GUILayout.Height(u * 0.6f));
        settings.showFps = GUILayout.Toggle(settings.showFps, " Show FPS (tap it for the log)", GUILayout.Height(u * 0.6f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Quality", GUILayout.Width(u * 2.4f));
        settings.quality = GUILayout.Toolbar(settings.quality, new[] { "Low", "Medium", "High" }, GUILayout.Height(u * 0.6f));
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Show log", GUILayout.Height(u * 0.8f))) logOpen = true;
        if (GUILayout.Button("Close", GUILayout.Height(u * 0.8f)))
        {
            settingsOpen = false;
            settings.Apply();
            settings.Save();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    static float Slider(string label, float value, float min, float max, float u)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + "  " + value.ToString("0.00"), GUILayout.Width(u * 3.6f));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Height(u * 0.5f));
        GUILayout.EndHorizontal();
        return value;
    }

    void DrawLog(float u)
    {
        Rect s = Safe;
        Rect panel = new Rect(s.x + u * 0.2f, s.y + u * 0.2f, s.width - u * 0.4f, s.height - u * 0.4f);
        Fill(panel, new Color(0f, 0f, 0f, 0.9f));
        smallStyle.fontSize = Mathf.RoundToInt(u * 0.16f);
        GUI.skin.button.fontSize = Mathf.RoundToInt(u * 0.22f);
        GUILayout.BeginArea(new Rect(panel.x + u * 0.2f, panel.y + u * 0.2f, panel.width - u * 0.4f, panel.height - u * 0.4f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Log: " + FlatLog.LogPath, smallStyle);
        if (GUILayout.Button("Close", GUILayout.Width(u * 2f), GUILayout.Height(u * 0.7f))) logOpen = false;
        GUILayout.EndHorizontal();
        logScroll = GUILayout.BeginScrollView(logScroll);
        string[] lines = FlatLog.Recent();
        for (int i = Mathf.Max(0, lines.Length - 150); i < lines.Length; i++) GUILayout.Label(lines[i], smallStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved) logScroll.y += t.deltaPosition.y * 2f;
        }
    }
}
