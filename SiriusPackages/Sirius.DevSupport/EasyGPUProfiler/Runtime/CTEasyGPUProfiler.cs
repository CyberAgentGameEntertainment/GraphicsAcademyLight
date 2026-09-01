using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sirius.DevSupport
{
    public class CTEasyGPUProfiler : MonoBehaviour
    {
        private class ListViewItem
        {
            public bool Root;
            public string PassName; // パス名
            public float GPUElapsedTime;    // このパスのGPU時間（子供がいる場合は合計）
            public float MaxGpuElapsedTime; // 子供がいる場合は子供の最大
            public Color FontColor;
            public List<ListViewItem> Children;
        }
        private class ListView{
            public List<ListViewItem> Items = new();
        }
        public static CTEasyGPUProfiler Instance { get; private set; }
        public Dictionary<Camera, List<RenderPassData>> RenderPassData { private get; set; } = new();
        private Vector2 _scrollPosition;
        [SerializeField] private Rect position = new (10, 10, 800, 600);
        [SerializeField] private Camera[] IgnoreCameras = Array.Empty<Camera>();
        [SerializeField]private bool IsScrollLocked = false; // スクロールロック状態
        [SerializeField]private bool ShowList = true;
        private float _expandedWindowHeight;
        private bool _resizing;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartSize;

        private float _updateTimer;
        private readonly ListView _listView = new();
        // --- 追加: タッチ・ドラッグスクロール用 ---
        private bool _isTouchScrolling;
        private Vector2 _lastTouchPos;
        private float _scrollVelocity;


        // 基準となる解像度
        private const float BASE_WIDTH = 1920f;
        private const float BASE_HEIGHT = 1080f;

        // フォントサイズの基準値
        private const float BASE_TITLE_FONT_SIZE = 28f;
        private const float BASE_LIST_FONT_SIZE = 24f;

        // レイアウトの基準値
        private const float BASE_MAX_TOTAL_WIDTH = 170f;
        private const float BASE_RIGHT_SECTION_WIDTH = 340f;

        private float GetScaledFontSize(float baseSize)
        {
            float screenScale = Mathf.Min(Screen.width / BASE_WIDTH, Screen.height / BASE_HEIGHT);
            return Mathf.Round(baseSize * screenScale);
        }

        private float GetScaledWidth(float baseWidth)
        {
            float screenScale = Mathf.Min(Screen.width / BASE_WIDTH, Screen.height / BASE_HEIGHT);
            return Mathf.Round(baseWidth * screenScale);
        }

        public void AddRenderPassData(Camera camera, List<RenderPassData> renderPassData)
        {
            if (RenderPassData == null) return;
            RenderPassData[camera] = renderPassData;
        }
        /// <summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 2個目以降は無効にする
                Debug.LogWarning("CTEasyGPUProfilerがシーンに複数設置されています");
                enabled = false;
                return;
            }
            if (!SystemInfo.supportsGpuRecorder) {
                Debug.LogWarning("この環境ではGPU Recordに対応していません");
                enabled = false;
                return;
            }
            _expandedWindowHeight = position.height;
            Instance = this;
        }
        private void Update()
        {
            if (RenderPassData == null) return;
            foreach (var cameraRenderPassData in RenderPassData)
            {
                var renderPassData = cameraRenderPassData.Value;
                foreach(var data in renderPassData){
                    var sampler = data.ProfilingSampler;
                    if (sampler == null) continue;
                    sampler.enableRecording = true; // レコード可能にする
                }
            }
            UpdateListView();
            RenderPassData = new();

            // --- 追加: リサイズ処理 ---
            if (_resizing)
            {
            #if ENABLE_INPUT_SYSTEM
                Vector2 mouse = Pointer.current?.position.ReadValue() ?? Vector2.zero;
            #else
                Vector2 mouse = Input.mousePosition;
            #endif
                mouse.y = Screen.height - mouse.y; // 座標系を合わせる
                Vector2 delta = mouse - _resizeStartMouse;
                position.width = Mathf.Max(200, _resizeStartSize.x + delta.x);
                position.height = Mathf.Max(100, _resizeStartSize.y + delta.y);
            }
        }
        private void OnGUI()
        {
            // ドラッグ可能なウィンドウ
            var dispPosition = position;
            if(!ShowList){
                dispPosition.height = 92;
            }

            position = GUI.Window(0, dispPosition, DrawWindowContents, "");
        }
        private void BuildListViewItem(ListViewItem listViewItem, ref int renderPassNo, ref int mergedPassNo,
            List<RenderPassData> renderPassDataList, ref float gpuTimeMaxOnCamera, ref float gpuTimeTotalOnCamera, Color fontColor)
        {
            var renderPassData = renderPassDataList[renderPassNo];
            var sampler = renderPassData.ProfilingSampler;
            renderPassNo++;
            listViewItem.FontColor = fontColor;
            if (renderPassData.BeginCTRenderScope)
            {
                // コアテク独自のRenderGraphのプロファイリングスコープの開始
                listViewItem.PassName = sampler.name;
                listViewItem.GPUElapsedTime = sampler.gpuElapsedTime;
                listViewItem.MaxGpuElapsedTime = 0.0f;
                listViewItem.Children = new List<ListViewItem>();
                // 子供の数を調べる
                var childCount = 0;
                for(int childRenderPassNo = renderPassNo; childRenderPassNo < renderPassDataList.Count; childRenderPassNo++)
                {
                    var renderPassDataChild = renderPassDataList[childRenderPassNo];
                    if (renderPassDataChild.EndCTRenderScope)
                        break;
                    if (!renderPassDataChild.MergeStart
                        && !renderPassDataChild.MergeEnd)
                    {
                        // マージの開始と終了はサンプラを保持していないのでスキップする
                        listViewItem.MaxGpuElapsedTime = Mathf.Max(
                            renderPassDataChild.ProfilingSampler.gpuElapsedTime,
                            listViewItem.MaxGpuElapsedTime
                        );
                    }
                    childCount++;
                }
                for (int childNo = 0; childNo < childCount; childNo++)
                {
                    var childItem = new ListViewItem();
                    listViewItem.Children.Add(childItem);
                    BuildListViewItem(childItem, ref renderPassNo, ref mergedPassNo, renderPassDataList,
                        ref gpuTimeMaxOnCamera, ref gpuTimeTotalOnCamera, fontColor);
                }
            }else if (renderPassData.EndCTRenderScope)
            {
                // コアテク独自のRenderGraphのプロファイリングスコープの終了
                listViewItem.PassName = "End CTRenderGraph Profiling Scope";

            }else if (renderPassData.MergeStart)
            {
                // マージ開始
                // コアテク独自のRenderGraphのプロファイリングスコープの開始
                listViewItem.PassName = $"Merged Pass: {mergedPassNo++}";
                listViewItem.GPUElapsedTime = 0.0f;
                listViewItem.MaxGpuElapsedTime = 0.0f;
                listViewItem.Children = new List<ListViewItem>();
                // 子供の数を調べる
                var childCount = 0;
                for(int childRenderPassNo = renderPassNo; childRenderPassNo < renderPassDataList.Count; childRenderPassNo++)
                {
                    var renderPassDataChild = renderPassDataList[childRenderPassNo];
                    if (renderPassDataChild.MergeEnd)
                        break;
                    childCount++;
                    listViewItem.GPUElapsedTime += renderPassDataChild.ProfilingSampler.gpuElapsedTime;
                    listViewItem.MaxGpuElapsedTime = Mathf.Max(
                        renderPassDataChild.ProfilingSampler.gpuElapsedTime,
                        listViewItem.MaxGpuElapsedTime
                        );
                }
                for (int childNo = 0; childNo < childCount; childNo++)
                {
                    var childItem = new ListViewItem();
                    listViewItem.Children.Add(childItem);
                    BuildListViewItem(childItem, ref renderPassNo, ref mergedPassNo, renderPassDataList,
                        ref gpuTimeMaxOnCamera, ref gpuTimeTotalOnCamera, fontColor);
                }
            }else if (renderPassData.MergeEnd)
            {
                listViewItem.PassName = "Merge End";
            }else {
                // スタンドアローンパス
                listViewItem.PassName = sampler.name;
                listViewItem.GPUElapsedTime = sampler.gpuElapsedTime;
                listViewItem.MaxGpuElapsedTime = sampler.gpuElapsedTime;
                gpuTimeMaxOnCamera = Mathf.Max(sampler.gpuElapsedTime, gpuTimeMaxOnCamera);
                gpuTimeTotalOnCamera += sampler.gpuElapsedTime;
            }
        }
        private void UpdateListView()
        {
            _updateTimer += Time.deltaTime;
            if(_updateTimer < 1.0f)
            {
                return; // 1秒ごとに更新
            }
            if (RenderPassData == null) return;
            var colors = new Color[] {
                Color.yellow,
                Color.cyan,
                Color.magenta,
                Color.green,
                Color.white,
            };
            _updateTimer = 0f;
            _listView.Items = new();
            int colorIndex = 0;
            foreach (var cameraRenderPassData in RenderPassData){
                if (IgnoreCameras.Contains(cameraRenderPassData.Key)) continue;
                var renderPassDataList = cameraRenderPassData.Value;
                var mergedPassNo = 0;
                var renderPassNo = 0;
                var rootItem = new ListViewItem();
                rootItem.Root = true;
                rootItem.PassName = cameraRenderPassData.Key.name;
                rootItem.FontColor = colors[colorIndex++ % colors.Length];
                _listView.Items.Add(rootItem);
                rootItem.Children = new List<ListViewItem>();
                while (renderPassNo < renderPassDataList.Count)
                {
                    var childItem = new ListViewItem();
                    rootItem.Children.Add(childItem);
                    BuildListViewItem(childItem, ref renderPassNo, ref mergedPassNo, renderPassDataList,
                        ref rootItem.MaxGpuElapsedTime, ref rootItem.GPUElapsedTime, rootItem.FontColor);
                }

            }
        }

        private void DrawListViewItem(ListViewItem listViewItem, GUIStyle style, int indent)
        {
            if(listViewItem.PassName == "Merge End" || listViewItem.PassName == "End CTRenderGraph Profiling Scope"){
                // マージ終了のパスとCTRenderGraphPfolilingScopeEndは表示しない
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUILayout.Label(listViewItem.PassName, style, GUILayout.Width(100), GUILayout.ExpandWidth(true));
            if (listViewItem.Children != null && listViewItem.Children.Count > 0)
            {
                var rightStyle = new GUIStyle(style);
                rightStyle.alignment = TextAnchor.MiddleRight;
                float scaledRightSectionWidth = GetScaledWidth(BASE_RIGHT_SECTION_WIDTH);
                float scaledMaxTotalWidth = GetScaledWidth(BASE_MAX_TOTAL_WIDTH);
                GUILayout.BeginHorizontal(GUILayout.Width(scaledRightSectionWidth));
                GUILayout.Label($"Max: {listViewItem.MaxGpuElapsedTime:F2} ms", rightStyle, GUILayout.Width(scaledMaxTotalWidth), GUILayout.ExpandWidth(false));
                GUILayout.Label($"Total: {listViewItem.GPUElapsedTime:F2} ms", rightStyle, GUILayout.Width(scaledMaxTotalWidth), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }
            else
            {
                var rightStyle = new GUIStyle(style);
                rightStyle.alignment = TextAnchor.MiddleRight;
                float scaledWidth = GetScaledWidth(200f); // 単一値表示時の幅も調整
                GUILayout.Label($"{listViewItem.GPUElapsedTime:F2} ms", rightStyle, GUILayout.Width(scaledWidth), GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();
            // 子供を表示
            if (listViewItem.Children == null) return;

            foreach (var childItem in listViewItem.Children)
            {
                DrawListViewItem(childItem, style, indent + 20);
            }
        }
        private void DrawScrollView(float scrollViewHeight, GUIStyle style)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, "box", GUILayout.Height(scrollViewHeight));

            // リストビューを表示
            GUILayout.BeginVertical();

            foreach (var listViewItem in _listView.Items)
            {
                var oldColor = GUI.color;
                GUI.color = listViewItem.FontColor;
                DrawListViewItem(listViewItem, style, 0);
                GUI.color = oldColor; // 元の色に戻す
            }
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }
        private void ControllScrollView(out float scrollViewHeight, float resizeHandleSize, float titleBarHeight)
        {
            scrollViewHeight = position.height - resizeHandleSize - titleBarHeight - 40; // 10は余白
            if (scrollViewHeight < 0) scrollViewHeight = 0;

            // --- 追加: スクロールビューの矩形を計算 ---
            var scrollViewRect = new Rect(0, titleBarHeight, position.width, scrollViewHeight);

            // スクロールがロックされている場合はスクロール処理をスキップ
            if (IsScrollLocked)
            {
                return;
            }

            // --- 追加: タッチ/マウスドラッグによるスクロール ---
            var e = Event.current;
            if (scrollViewRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    _isTouchScrolling = true;
                    _lastTouchPos = e.mousePosition;
                    _scrollVelocity = 0f;
                    e.Use();
                }
                else if (_isTouchScrolling && e.type == EventType.MouseDrag && e.button == 0)
                {
                    var deltaY = e.mousePosition.y - _lastTouchPos.y;
                    _scrollPosition.y -= deltaY;
                    _lastTouchPos = e.mousePosition;
                    _scrollVelocity = -deltaY / Mathf.Max(Time.deltaTime, 0.016f); // 慣性用
                    e.Use();
                }
                else if (_isTouchScrolling && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow))
                {
                    _isTouchScrolling = false;
                    e.Use();
                }
            }
            // --- 追加: 慣性スクロール ---
            if (!_isTouchScrolling && Mathf.Abs(_scrollVelocity) > 0.1f)
            {
                _scrollPosition.y += _scrollVelocity * Time.deltaTime;
                _scrollVelocity = Mathf.Lerp(_scrollVelocity, 0, 5f * Time.deltaTime); // 減衰
            }
            // 範囲チェック（0未満に行かないように）
            if (_scrollPosition.y < 0) _scrollPosition.y = 0;
        }
        private void ControllResize(out Rect handleRect, float resizeHandleSize)
        {
            // --- ここからリサイズハンドル ---
            handleRect = new Rect(position.width - resizeHandleSize,
                position.height - resizeHandleSize, resizeHandleSize, resizeHandleSize);

            var e = Event.current;
            if (!_resizing && e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
            {
                _resizing = true;
                _resizeStartMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                _resizeStartSize = new Vector2(position.width, position.height);
                e.Use();
            }
            if (_resizing && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow))
            {
                _resizing = false;
                e.Use();
            }
        }
        private void DrawTitleBar(float titleBarHeight, Action onToggleDispList)
        {
            GUILayout.BeginHorizontal();
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = (int)GetScaledFontSize(BASE_TITLE_FONT_SIZE);
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.contentOffset = new Vector2(4, -14); // テキストの位置を上に調整

            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = (int)GetScaledFontSize(BASE_TITLE_FONT_SIZE);
            buttonStyle.fixedWidth = 70;  // 50から70に増加
            buttonStyle.fixedHeight = 50; // 36から50に増加
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.padding = new RectOffset(0, 0, -4, 0);

            // ボタンを先に描画
            if (GUILayout.Button(ShowList ? "-" : "□", buttonStyle, GUILayout.Width(70), GUILayout.Height(50)))
            {
                onToggleDispList();
            }

            // スクロールロックボタンを追加
            var lockButtonStyle = new GUIStyle(buttonStyle);
            lockButtonStyle.normal.textColor = IsScrollLocked ? Color.red : Color.white;
            lockButtonStyle.hover.textColor = IsScrollLocked ? Color.red : Color.white;
            if (GUILayout.Button(IsScrollLocked ? "L" : "U", lockButtonStyle, GUILayout.Width(70), GUILayout.Height(50)))
            {
                IsScrollLocked = !IsScrollLocked;
            }

            // その右にタイトルラベルを描画
            GUILayout.Label("CT Easy GPU Profiler", titleStyle, GUILayout.Height(titleBarHeight), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
        private void DrwaResizeHandle(Rect handleRect)
        {
            // リサイズハンドルを表示
            var resizeStyle = new GUIStyle(GUI.skin.label);
            resizeStyle.alignment = TextAnchor.MiddleCenter;
            resizeStyle.fontSize = (int)GetScaledFontSize(BASE_LIST_FONT_SIZE + 20);

            GUI.Label(handleRect, "↘", resizeStyle);
            GUI.Label(handleRect, "↖", resizeStyle);
        }
        private void DrawWindowContents(int windowID)
        {
            var titleBarHeight = 80f;
            var resizeHandleSize = 80f;
            ControllScrollView(out var scrollViewHeight, resizeHandleSize, titleBarHeight);
            ControllResize(out var handleRect, resizeHandleSize);

            // タイトルバーとトグルボタン（大きめ）
            DrawTitleBar(titleBarHeight,()=>{
                if(ShowList){
                    // 開いているときのウィンドウの高さを記憶する
                    _expandedWindowHeight = position.height;
                }else{
                    // ウィンドウの高さを元に戻す
                    position.height = _expandedWindowHeight;
                }
                ShowList = !ShowList;
            });

            if (!ShowList)
            {
                GUI.DragWindow();
                return;
            }

            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = (int)GetScaledFontSize(BASE_LIST_FONT_SIZE);

            DrawScrollView(scrollViewHeight, style);
            DrwaResizeHandle(handleRect);

            // --- ここまでリサイズハンドル ---
            // タイトルバーの領域を定義
            var titleBarRect = new Rect(0, 0, position.width, 160);

            // タイトルバー以外の領域をクリックした場合はドラッグを無効化
            if (Event.current.type == EventType.MouseDown && !titleBarRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
            }

            GUI.DragWindow();
        }
    }
}
