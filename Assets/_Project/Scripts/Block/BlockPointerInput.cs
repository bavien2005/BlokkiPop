using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>One owner per gesture. UI and world blocks share the new Input System; no legacy mouse emulation on Android.</summary>
[DefaultExecutionOrder(-900)]
public sealed class BlockPointerInput : MonoBehaviour
{
    private static BlockPointerInput instance;
    private readonly List<RaycastResult> uiHits = new();
    private Block dragging;
    private int touchId;
    private bool touchGesture, ownsEnhancedTouch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null) new GameObject("Block Pointer Input").AddComponent<BlockPointerInput>();
    }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        EnhancedTouchSupport.Enable(); ownsEnhancedTouch = true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || AdsPrivacyPanel.IsModalOpen || PopGameplayUI.IsModalOpen ||
            AdsService.Instance != null && AdsService.Instance.IsShowing ||
            GameFlowManager._instance != null && GameFlowManager._instance.IsGameOver)
        { CancelGesture(); return; }

        var touches = Touch.activeTouches;
        if (dragging != null && touchGesture)
        {
            foreach (Touch touch in touches)
            {
                if (touch.touchId != touchId) continue;
                if (touch.phase == TouchPhase.Canceled) CancelGesture();
                else if (touch.phase == TouchPhase.Ended) FinishGesture(touch.screenPosition);
                else dragging.MovePointerDrag(touch.screenPosition);
                return;
            }
            CancelGesture(); // Lost/device-disconnected touches never silently place a block.
            return;
        }
        if (dragging != null)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) CancelGesture();
            else if (mouse.leftButton.wasReleasedThisFrame) FinishGesture(mouse.position.ReadValue());
            else if (mouse.leftButton.isPressed) dragging.MovePointerDrag(mouse.position.ReadValue());
            else CancelGesture();
            return;
        }
        foreach (Touch touch in touches)
            if (touch.phase == TouchPhase.Began && TryBegin(touch.screenPosition))
            { touchGesture = true; touchId = touch.touchId; return; }
        // EnhancedTouch owns touch input. Ignore emulated/duplicate mouse events while any finger is present.
        if (touches.Count == 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            TryBegin(Mouse.current.position.ReadValue())) touchGesture = false;
    }

    private bool TryBegin(Vector2 point)
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.pixelRect.Contains(point)) return false;
        EventSystem events = EventSystem.current;
        if (events != null)
        {
            uiHits.Clear(); events.RaycastAll(new PointerEventData(events) { position = point }, uiHits);
            foreach (RaycastResult hit in uiHits)
                if (hit.gameObject.GetComponentInParent<Selectable>() != null || hit.gameObject.GetComponentInParent<ScrollRect>() != null)
                    return false; // Decorative score/background Images must not block the world-space tray.
        }
        Vector2 world = camera.ScreenToWorldPoint(point);
        foreach (Collider2D collider in Physics2D.OverlapPointAll(world))
        {
            Block block = collider.GetComponentInParent<Block>();
            if (block == null || !block.BeginPointerDrag(point)) continue;
            dragging = block; return true;
        }
        return false;
    }
    private void FinishGesture(Vector2 point)
    {
        Block block = dragging; dragging = null;
        block?.EndPointerDrag(point);
    }
    private void CancelGesture()
    {
        if (dragging != null) dragging.CancelPointerDrag();
        dragging = null;
    }
    private void OnApplicationPause(bool paused) { if (paused) CancelGesture(); }
    private void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
    private void OnDestroy()
    {
        if (instance != this) return;
        CancelGesture(); instance = null;
        if (ownsEnhancedTouch) { EnhancedTouchSupport.Disable(); ownsEnhancedTouch = false; }
    }
}
