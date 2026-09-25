using System;
using Il2CppViews.Toolbar;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace XRay
{
    static class XRayBar
    {
        const string BarName = "XRayBar";

        static RectTransform _bar;
        static RectTransform _row;
        static ToolbarItemSlotView _bones;
        static ToolbarItemSlotView _organs;
        static bool _bonesLit;
        static bool _organsLit;
        static bool _failed;
        static bool _loggedPlace;
        static Canvas _canvas;
        static Sprite _boneIcon;
        static Sprite _organIcon;

        internal static void Reset()
        {
            _bar = null;
            _row = null;
            _bones = null;
            _organs = null;
            _bonesLit = false;
            _organsLit = false;
            _failed = false;
            _loggedPlace = false;
            _canvas = null;
        }

        internal static void Tick(Phx.Hotkey bonesKey, Phx.Hotkey organsKey, bool bonesOn, bool organsOn, Action toggleBones, Action toggleOrgans)
        {
            if (_bones == null || _organs == null)
            {
                if (_failed)
                    return;
                Build(bonesKey, organsKey, toggleBones, toggleOrgans);
            }
            if (_bones == null || _organs == null)
                return;

            if (_bar != null && _row != null && _bar.gameObject.activeSelf != _row.gameObject.activeInHierarchy)
                _bar.gameObject.SetActive(_row.gameObject.activeInHierarchy);

            Align();
            Paint(_bones, bonesKey, bonesOn, ref _bonesLit);
            Paint(_organs, organsKey, organsOn, ref _organsLit);
        }

        static void Align()
        {
            if (_bar == null || _bones == null || _organs == null || _canvas == null)
                return;

            RectTransform canvasRect = _canvas.transform.TryCast<RectTransform>();
            if (canvasRect == null)
                return;
            if (!TryMeasure(canvasRect, out Vector2 topRight, out float slotW, out float slotH))
                return;

            float gap = slotW * 0.08f;
            float drop = slotH * -0.22f;
            _bar.anchorMin = Vector2.zero;
            _bar.anchorMax = Vector2.zero;
            _bar.pivot = new Vector2(1f, 0f);
            _bar.localScale = Vector3.one;
            _bar.sizeDelta = new Vector2(slotW * 2f + gap, slotH);
            _bar.anchoredPosition = new Vector2(topRight.x, topRight.y - drop);
            _bar.SetAsLastSibling();
            PlaceSlot(_bones, new Vector2(-(slotW + gap + slotW * 0.5f), slotH * 0.5f), slotW, slotH);
            PlaceSlot(_organs, new Vector2(-slotW * 0.5f, slotH * 0.5f), slotW, slotH);

            if (!_loggedPlace)
            {
                _loggedPlace = true;
                MelonLogger.Msg("[X-Ray] Toolbar row placed at " + _bar.anchoredPosition + " slot " + slotW + "x" + slotH + ".");
            }
        }

        static bool TryMeasure(RectTransform space, out Vector2 topRight, out float slotW, out float slotH)
        {
            topRight = default;
            slotW = 0f;
            slotH = 0f;
            ToolbarView view = Object.FindFirstObjectByType<ToolbarView>();
            if (view == null || view.m_slotsViews == null)
                return false;

            float right = float.NegativeInfinity;
            float top = float.NegativeInfinity;
            float width = 0f;
            float height = 0f;
            int used = 0;
            for (int i = 0; i < view.m_slotsViews.Length; i++)
            {
                if (!SlotBox(view.m_slotsViews[i], space, out Vector2 bottomLeft, out Vector2 far))
                    continue;
                float w = far.x - bottomLeft.x;
                float h = far.y - bottomLeft.y;
                if (w < 8f || h < 8f)
                    continue;
                if (far.x > right)
                    right = far.x;
                if (far.y > top)
                    top = far.y;
                width = w;
                height = h;
                used++;
            }
            if (used == 0)
                return false;

            CollectHudEdge(space, width, height, top, ref right);
            topRight = new Vector2(right, top);
            slotW = width;
            slotH = height;
            return true;
        }

        static void CollectHudEdge(RectTransform space, float slotWidth, float slotHeight, float rowTop, ref float right)
        {
            if (_canvas == null)
                return;
            RectTransform[] rects;
            try { rects = _canvas.GetComponentsInChildren<RectTransform>(false); }
            catch { return; }
            float rowBottom = rowTop - slotHeight;
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null || rect == space || (_bar != null && (rect == _bar || rect.IsChildOf(_bar))))
                    continue;
                if (!Box(rect, space, out Vector2 bottomLeft, out Vector2 far))
                    continue;
                float width = far.x - bottomLeft.x;
                float height = far.y - bottomLeft.y;
                if (width < slotWidth * 0.6f || width > slotWidth * 1.6f || height < slotHeight * 0.6f || height > slotHeight * 1.6f)
                    continue;
                float overlap = Mathf.Min(far.y, rowTop) - Mathf.Max(bottomLeft.y, rowBottom);
                if (overlap < slotHeight * 0.5f)
                    continue;
                if (far.x > right)
                    right = far.x;
            }
        }

        static bool SlotBox(ToolbarItemSlotView slot, RectTransform space, out Vector2 bottomLeft, out Vector2 topRight)
        {
            bottomLeft = default;
            topRight = default;
            if (slot == null || !slot.gameObject.activeInHierarchy)
                return false;
            if (_bar != null && slot.transform.IsChildOf(_bar))
                return false;
            return Box(slot.transform.TryCast<RectTransform>(), space, out bottomLeft, out topRight);
        }

        static bool Box(RectTransform rect, RectTransform space, out Vector2 bottomLeft, out Vector2 topRight)
        {
            bottomLeft = default;
            topRight = default;
            if (rect == null || space == null || !rect.gameObject.activeInHierarchy)
                return false;
            Rect area = rect.rect;
            if (area.width < 4f || area.height < 4f)
                return false;
            Vector3 worldMin = rect.TransformPoint(new Vector3(area.xMin, area.yMin, 0f));
            Vector3 worldMax = rect.TransformPoint(new Vector3(area.xMax, area.yMax, 0f));
            Vector2 localMin = space.InverseTransformPoint(worldMin);
            Vector2 localMax = space.InverseTransformPoint(worldMax);
            Vector2 origin = space.rect.min;
            bottomLeft = Vector2.Min(localMin, localMax) - origin;
            topRight = Vector2.Max(localMin, localMax) - origin;
            return topRight.x - bottomLeft.x > 4f && topRight.y - bottomLeft.y > 4f;
        }

        static void PlaceSlot(ToolbarItemSlotView slot, Vector2 anchored, float width, float height)
        {
            RectTransform rect = slot.transform.TryCast<RectTransform>();
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = anchored;
        }

        static void Build(Phx.Hotkey bonesKey, Phx.Hotkey organsKey, Action toggleBones, Action toggleOrgans)
        {
            ToolbarView view = Object.FindFirstObjectByType<ToolbarView>();
            if (view == null || view.m_slotsViews == null || view.m_slotsViews.Length == 0)
                return;

            ToolbarItemSlotView proto = null;
            for (int i = 0; i < view.m_slotsViews.Length; i++)
            {
                if (view.m_slotsViews[i] != null)
                {
                    proto = view.m_slotsViews[i];
                    break;
                }
            }
            if (proto == null || proto.transform.parent == null)
                return;

            RectTransform row = proto.transform.parent.TryCast<RectTransform>();
            if (row == null || row.parent == null)
                return;

            try
            {
                Canvas canvas = row.GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;
                _canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

                GameObject old = GameObject.Find(BarName);
                if (old != null)
                    Object.Destroy(old);

                float slotSize = SlotSize(proto);
                var barObject = new GameObject(BarName);
                _bar = barObject.AddComponent<RectTransform>();
                _bar.SetParent(_canvas.transform, false);
                _bar.anchorMin = Vector2.zero;
                _bar.anchorMax = Vector2.zero;
                _bar.pivot = new Vector2(1f, 0f);
                _bar.sizeDelta = new Vector2(slotSize * 2.08f, slotSize);
                _bar.localScale = Vector3.one;
                _bar.SetAsLastSibling();
                _row = row;

                _boneIcon = _boneIcon != null ? _boneIcon : DrawBone();
                _organIcon = _organIcon != null ? _organIcon : DrawHeart();
                _bones = MakeSlot(proto, _bar, "XRay_Bones", _boneIcon, bonesKey, toggleBones, slotSize);
                _organs = MakeSlot(proto, _bar, "XRay_Organs", _organIcon, organsKey, toggleOrgans, slotSize);
                _bonesLit = false;
                _organsLit = false;
                MelonLogger.Msg("[X-Ray] Toolbar row added above the game slots.");
            }
            catch (Exception e)
            {
                _failed = true;
                MelonLogger.Warning("[X-Ray] Could not add the toolbar row: " + e.Message);
            }
        }

        static float SlotSize(ToolbarItemSlotView proto)
        {
            RectTransform sample = proto.transform.TryCast<RectTransform>();
            if (sample == null)
                return 78f;
            float width = sample.sizeDelta.x;
            if (width < 40f || width > 140f)
                width = sample.rect.width;
            if (width < 40f || width > 140f)
                return 78f;
            return width;
        }

        static ToolbarItemSlotView MakeSlot(ToolbarItemSlotView proto, RectTransform parent, string name, Sprite icon, Phx.Hotkey key, Action toggle, float slotSize)
        {
            ToolbarItemSlotView slot = Object.Instantiate(proto, parent, false);
            slot.gameObject.name = name;
            slot.gameObject.SetActive(true);
            try { slot.SetAvailable(true); } catch { }
            try { slot.Deselect(); } catch { }

            RectTransform rect = slot.transform.TryCast<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(slotSize, slotSize);
                LayoutElement element = slot.gameObject.GetComponent<LayoutElement>();
                if (element == null)
                    element = slot.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = slotSize;
                element.preferredHeight = slotSize;
                element.minWidth = slotSize;
                element.minHeight = slotSize;
                element.flexibleWidth = 0f;
                element.flexibleHeight = 0f;
            }

            AddLogo(slot, icon);

            if (slot.m_keySlot != null)
            {
                try { slot.m_keySlot.SetKeyTitle(Letter(key)); } catch { }
            }

            Button button = slot.GetComponent<Button>();
            if (button == null)
                button = slot.GetComponentInChildren<Button>(true);
            if (button == null)
                button = slot.gameObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            UnityAction click = (UnityAction)toggle;
            button.onClick.AddListener(click);
            return slot;
        }

        static void Paint(ToolbarItemSlotView slot, Phx.Hotkey key, bool on, ref bool lit)
        {
            if (slot == null)
                return;
            if (slot.m_keySlot != null)
            {
                try { slot.m_keySlot.SetKeyTitle(Letter(key)); } catch { }
            }
            if (on == lit)
                return;
            lit = on;
            try
            {
                if (on)
                    slot.Select();
                else
                    slot.Deselect();
            }
            catch { }
        }

        static void AddLogo(ToolbarItemSlotView slot, Sprite icon)
        {
            if (slot == null || icon == null)
                return;

            var logoObject = new GameObject("XRayLogo");
            RectTransform rect = logoObject.AddComponent<RectTransform>();
            rect.SetParent(slot.transform, false);
            rect.anchorMin = new Vector2(0.16f, 0.12f);
            rect.anchorMax = new Vector2(0.84f, 0.7f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            Image image = logoObject.AddComponent<Image>();
            image.sprite = icon;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.SetAsLastSibling();
            if (slot.m_keySlot != null)
                slot.m_keySlot.transform.SetAsLastSibling();
        }

        static string Letter(Phx.Hotkey key)
        {
            if (key == null)
                return "";
            string name = key.Current.ToString();
            if (name.Length == 1)
                return name;
            if (name.StartsWith("Digit") && name.Length == 6)
                return name.Substring(5);
            return name;
        }

        static Sprite DrawBone()
        {
            const int size = 64;
            Texture2D tex = NewTexture(size);
            tex.filterMode = FilterMode.Point;
            var ink = Color.white;
            FillCircle(tex, 32, 50, 11, ink);
            FillCircle(tex, 32, 50, 6, new Color(0f, 0f, 0f, 0f));
            FillRect(tex, 27, 40, 37, 46, ink);
            FillRect(tex, 28, 12, 36, 44, ink);
            Rib(tex, 40, 24, 4, ink);
            Rib(tex, 32, 22, 4, ink);
            Rib(tex, 24, 18, 3, ink);
            Rib(tex, 17, 13, 3, ink);
            return Finish(tex, "XRayBone");
        }

        static void Rib(Texture2D tex, int y, int reach, int thick, Color color)
        {
            int half = Mathf.Max(1, thick / 2);
            for (int x = 32 - reach; x <= 32 + reach; x++)
            {
                int dx = x - 32;
                int drop = reach <= 0 ? 0 : (dx * dx) / (reach * 2);
                int py = y - drop;
                for (int t = -half; t <= half; t++)
                {
                    int px = x;
                    int row = py + t;
                    if (px >= 0 && px < tex.width && row >= 0 && row < tex.height)
                        tex.SetPixel(px, row, color);
                }
            }
        }

        static Sprite DrawHeart()
        {
            const int size = 64;
            Texture2D tex = NewTexture(size);
            var ink = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hx = (x - 31.5f) / 26f;
                    float hy = (y - 28f) / 26f;
                    float a = hx * hx + hy * hy - 1f;
                    if (a * a * a - hx * hx * hy * hy * hy <= 0f)
                        tex.SetPixel(x, y, ink);
                }
            }
            return Finish(tex, "XRayOrgan");
        }

        static Texture2D NewTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);
            }
            return tex;
        }

        static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color color)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                    tex.SetPixel(x, y, color);
            }
        }

        static void FillCircle(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        static Sprite Finish(Texture2D tex, string name)
        {
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            tex.name = name;
            sprite.name = name;
            tex.hideFlags = HideFlags.HideAndDontSave;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
