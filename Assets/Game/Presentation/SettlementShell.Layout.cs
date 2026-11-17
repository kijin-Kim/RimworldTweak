using System;
using System.Collections.Generic;
using System.Linq;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementShell
    {
        private RectTransform colonistBar, portraitViewport, resources, timeControls;
        private RectTransform inspectContent, panelContent, alertContent, alerts;
        private ScrollRect portraitScroll, commandScroll;
        private const float WorkTableMinimumWidth = 122 + 5 * 92 + 5 * 6;
        private readonly Dictionary<ResourceKind, Text> resourceTotals = new Dictionary<ResourceKind, Text>();
        private Text timeStatus, selectionSummary, resourceBreakdown;
        private RectTransform resourceTooltip, portraitTooltip;
        private Text portraitFullName;
        private readonly Dictionary<RectTransform, string> truncatedPortraits = new Dictionary<RectTransform, string>();
        private RectTransform selectionCard, selectionCommands;
        private bool detailOpen;
        private const float PortraitWidth = 62;
        private const float PortraitStride = 70;
        private const float PortraitHeight = 72;
        private int lastPortraitId=-1;
        private string architectCategory;
        private Text architectDescription;
        private float lastPortraitClick=-10;
        private void BuildShell()
        {
            canvasRoot = new GameObject("Settlement HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<RectTransform>();
            canvasRoot.SetParent(transform,false);
            canvasRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            scaler=canvasRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            BuildEdgeHud();
            selectionCard = Box("Selection summary", canvasRoot);
            selectionSummary = Label("Selected target", selectionCard, 17);
            Stretch(selectionSummary.rectTransform, 10);
            selectionCommands = Box("Selection commands", canvasRoot, Color.clear);
            inspection=Box("Inspector",canvasRoot);inspectContent=Scroll("Inspector scroll",inspection);
            panel=Box("Command panel",canvasRoot);panelContent=Scroll("Command scroll",panel);
            commandScroll = panelContent.GetComponentInParent<ScrollRect>();
            tabs=Box("Tabs",canvasRoot);
            ButtonAt("tab.build","건축가",tabs,0,0,144,40,()=>ToggleTab("architect"));
            ButtonAt("tab.work","작업 배분",tabs,152,0,144,40,()=>ToggleTab("work"));
            ButtonAt("tab.menu","메뉴 [Esc]",tabs,454,0,160,44,ShowPauseMenu);
            messages=Box("Feedback",canvasRoot,new Color(.10f,.13f,.12f,.95f));
            messages.GetComponent<Image>().raycastTarget=false;
            alerts=Box("Work notices",canvasRoot);alertContent=Scroll("Notice scroll",alerts);
            feedbackLabel=Label("Feedback text",messages,16);feedbackLabel.alignment=TextAnchor.MiddleCenter;Stretch(feedbackLabel.rectTransform,10);
            feedbackLabel.rectTransform.offsetMin=new Vector2(10,4);
            feedbackLabel.rectTransform.offsetMax=new Vector2(-10,-4);
            BuildColonistBar();BuildInspector();
        }
        private void BuildEdgeHud()
        {
            resources = Box("Resources", canvasRoot);
            resources.GetComponent<Image>().raycastTarget = false;
            var row = 0;
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
            {
                var icon = Box("Resource icon " + kind, resources, Color.clear);
                Place(icon, 8, row * 26 + 3, 20, 20);
                if (visualCatalog != null && visualCatalog.TryGet(VisualCatalog.ResourceKey(kind), out var visual))
                {
                    var image = icon.GetComponent<Image>();
                    image.sprite = visual.Sprite;
                    image.color = Color.white;
                    image.preserveAspect = true;
                }
                var total = Label("Resource total " + kind, resources, 16);
                total.alignment = TextAnchor.MiddleLeft;
                Place(total.rectTransform, 34, row * 26, 100, 26);
                resourceTotals.Add(kind, total);
                row++;
            }

            resourceTooltip = Box("Resource breakdown", canvasRoot);
            resourceTooltip.GetComponent<Image>().raycastTarget = false;
            resourceBreakdown = Label("Resource breakdown text", resourceTooltip, 16);
            Stretch(resourceBreakdown.rectTransform, 6);
            resourceTooltip.gameObject.SetActive(false);

            portraitViewport = Box("Portrait viewport", canvasRoot, Color.clear);
            portraitViewport.gameObject.AddComponent<RectMask2D>();
            colonistBar = Box("Colonists", portraitViewport, Color.clear);
            portraitScroll = portraitViewport.gameObject.AddComponent<ScrollRect>();
            portraitScroll.viewport = portraitViewport;
            portraitScroll.content = colonistBar;
            portraitScroll.vertical = false;
            portraitScroll.movementType = ScrollRect.MovementType.Clamped;

            portraitTooltip = Box("Portrait full name", canvasRoot);
            portraitTooltip.GetComponent<Image>().raycastTarget = false;
            portraitFullName = Label("Full name", portraitTooltip, 16);
            portraitFullName.alignment = TextAnchor.MiddleCenter;
            Stretch(portraitFullName.rectTransform, 3);
            portraitTooltip.gameObject.SetActive(false);

            timeControls = Box("Time controls", canvasRoot);
            timeStatus = Label("Time status", timeControls, 16);
            timeStatus.alignment = TextAnchor.MiddleCenter;
            Place(timeStatus.rectTransform, 0, 0, 176, 28);
            ButtonAt("pause", "재생", timeControls, 0, 30, 56, 32, () => Runner.SetPaused(!Runner.IsPaused));
            ButtonAt("speed1", "1배속", timeControls, 60, 30, 56, 32, () => Runner.SetSpeed(1));
            ButtonAt("speed3", "3배속", timeControls, 120, 30, 56, 32, () => Runner.SetSpeed(3));
            foreach (var key in new[] { "pause", "speed1", "speed3" })
            {
                var caption = buttons[key].GetComponentInChildren<Text>();
                caption.fontSize = 15;
                Stretch(caption.rectTransform, 1);
            }
        }

        private void BuildColonistBar()
        {
            Clear(colonistBar);
            refreshers.Clear();
            truncatedPortraits.Clear();
            lastPortraitId = -1;
            var index = 0;
            foreach (var pawn in Runner.Session.World.Colonists)
            {
                var id = pawn.Id;
                var button = ButtonAt("pawn." + id, "", colonistBar, index * PortraitStride, 0, PortraitWidth, PortraitHeight, () =>
                {
                    var doubleClick = lastPortraitId == id && Time.unscaledTime - lastPortraitClick <= .3f;
                    SelectPawn(id);
                    if (doubleClick) FocusTarget(pawn.Position);
                    lastPortraitId = id;
                    lastPortraitClick = Time.unscaledTime;
                });
                var caption = button.GetComponentInChildren<Text>();
                caption.fontSize = 14;
                caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(caption.rectTransform, 2, 50, PortraitWidth - 4, 22);
                var fullName = "정착민 " + id;
                caption.text = FitPortraitName(caption, fullName);
                if (caption.text != fullName) truncatedPortraits.Add((RectTransform)button.transform, fullName);
                if (visualCatalog != null && visualCatalog.TryGet("pawn.front", out var portraitVisual))
                {
                    var portrait = Box("Portrait", button.transform, Color.clear);
                    Place(portrait, 7, 2, 48, 48);
                    var image = portrait.GetComponent<Image>();
                    image.sprite = portraitVisual.Sprite;
                    image.color = WorldMapView.PawnColor(id);
                    image.preserveAspect = true;
                }
                refreshers.Add(() =>
                {
                    button.GetComponent<Image>().color = SelectedPawnId == id
                        ? new Color(.30f,.40f,.26f)
                        : pawn.IsDrafted ? new Color(.27f,.34f,.39f) : new Color(.15f,.19f,.19f);
                });
                index++;
            }

            Place(colonistBar, 0, 0, Mathf.Max(0, index * PortraitStride - 8), PortraitHeight);
            LayoutPortraits(canvasRoot.rect.width);
        }

        private static string FitPortraitName(Text caption, string fullName)
        {
            var generator = new TextGenerator();
            var settings = caption.GetGenerationSettings(Vector2.zero);
            settings.scaleFactor = 1;
            var width = caption.rectTransform.rect.width;
            if (generator.GetPreferredWidth(fullName, settings) <= width) return fullName;
            for (var length = fullName.Length - 1; length > 0; length--)
            {
                var shortened = fullName.Substring(0, length).TrimEnd() + "…";
                if (generator.GetPreferredWidth(shortened, settings) <= width) return shortened;
            }
            return "…";
        }

        private void RefreshPortraitTooltip()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (!IsInputBlocked && mouse != null && !MapInput.IsCapturing)
            {
                var pointer = mouse.position.ReadValue();
                if (RectTransformUtility.RectangleContainsScreenPoint(portraitViewport, pointer))
                {
                    foreach (var portrait in truncatedPortraits)
                    {
                        if (!RectTransformUtility.RectangleContainsScreenPoint(portrait.Key, pointer)) continue;
                        var center = RectTransformUtility.WorldToScreenPoint(null, portrait.Key.TransformPoint(portrait.Key.rect.center));
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, center, null, out var canvasPoint);
                        const float width = 224;
                        var x = Mathf.Clamp(canvasPoint.x - canvasRoot.rect.xMin - width / 2, 8, canvasRoot.rect.width - width - 8);
                        Place(portraitTooltip, x, 84, width, 36);
                        portraitFullName.text = portrait.Value;
                        if (!portraitTooltip.gameObject.activeSelf) portraitTooltip.SetAsLastSibling();
                        portraitTooltip.gameObject.SetActive(true);
                        return;
                    }
                }
            }
            portraitTooltip.gameObject.SetActive(false);
        }

        private void LayoutPortraits(float width)
        {
            var available = Mathf.Max(0, width - 304);
            var viewportWidth = Mathf.Min(colonistBar.rect.width, available);
            Place(portraitViewport, (width - viewportWidth) / 2, 8, viewportWidth, PortraitHeight);
            var overflowing = colonistBar.rect.width > viewportWidth;
            portraitScroll.enabled = overflowing;
            portraitViewport.GetComponent<Image>().raycastTarget = overflowing;
        }

        private void RefreshLayout()
        {
            var scale = Runner.Settings.UiScalePercent;
            scaler.referenceResolution = new Vector2(1920,1080) * 100f / scale;
            Canvas.ForceUpdateCanvases();
            var size = canvasRoot.rect.size;
            var width = size.x;
            var height = size.y;
            var bottom = Mathf.Min(320, height * .39f);
            var occupiedHeight = panel.gameObject.activeSelf ? bottom
                : inspection.gameObject.activeSelf ? 192 + Mathf.Min(360, height - 282)
                : selectionCard.gameObject.activeSelf ? 188 : 44;
            var messageWidth = Mathf.Min(660, width - 244);
            Place(messages, (width - 236 - messageWidth) / 2, height - occupiedHeight - 40, messageWidth, 36);
            messages.gameObject.SetActive(!IsMenuOpen && !string.IsNullOrEmpty(Feedback) && !HasArchitectTool);
            if (size == layoutSize && scale == layoutScale) return;
            layoutSize = size;
            layoutScale = scale;
            Place(resources, 8, 8, 136, 78);
            Place(resourceTooltip, 8, 90, 272, 88);
            LayoutPortraits(width);
            Place(timeControls, width - 184, height - 106, 176, 62);
            var alertHeight = Mathf.Clamp(height - 204, 72, 180);
            Place(alerts, width - 228, height - 118 - alertHeight, 220, alertHeight);
            Place(tabs, 0, height - 40, width, 40);
            tabs.GetComponent<Image>().raycastTarget = false;
            Place((RectTransform)buttons["tab.menu"].transform, width - 120, 0, 120, 40);
            LayoutSelection(width, height);
            Place(panel, 8, height - bottom, Mathf.Min(880, width - 244), bottom - 44);
            LayoutCommandScroll();
            Map?.SetViewport(new Rect(0,0,1,1));
            LayoutMenu();
        }

        private RectTransform Box(string name,Transform parent,Color? color=null)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            var image=r.gameObject.AddComponent<Image>();image.color=color??PanelColor;image.raycastTarget=color!=Color.clear;
            if(!color.HasValue&&visualCatalog!=null&&visualCatalog.TryGet("ui.panel",out var panelVisual))
            {image.sprite=panelVisual.Sprite;image.type=Image.Type.Sliced;image.color=Color.white;}
            return r;
        }
        private static void Place(RectTransform r,float x,float y,float w,float h)
        {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        private static void Stretch(RectTransform r,float margin=0)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(margin,margin);r.offsetMax=new Vector2(-margin,-margin);}
        private Text Label(string name,Transform parent,int size=18)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=TextColor;t.supportRichText=false;
            t.raycastTarget=false;t.alignment=TextAnchor.UpperLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
            return t;
        }
        private Button ButtonAt(string key,string caption,Transform parent,float x,float y,float w,float h,Action action)
        {
            var r=Box(key,parent,new Color(.17f,.215f,.22f));Place(r,x,y,w,h);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            var colors=b.colors;colors.highlightedColor=new Color(.78f,.87f,.73f);colors.selectedColor=new Color(.78f,.87f,.73f);b.colors=colors;
            var text=Label("Caption",r,17);text.text=caption;text.alignment=TextAnchor.MiddleCenter;Stretch(text.rectTransform,4);
            b.onClick.AddListener(()=>{if(!Runner.SaveBusy)action();});buttons[key]=b;return b;
        }
        private RectTransform Scroll(string name,RectTransform parent)
        {
            var outer=Box(name,parent,Color.clear);Stretch(outer,10);
            var viewport=Box("Viewport",outer,new Color(.08f,.105f,.12f,1));Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var group=content.gameObject.AddComponent<VerticalLayoutGroup>();group.spacing=7;group.childControlWidth=true;group.childControlHeight=true;group.childForceExpandWidth=true;group.childForceExpandHeight=false;
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=outer.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            return content;
        }
        private Text RowLabel(Transform parent,string caption,int height=56,int size=18)
        {
            var label=Label("Text",parent,size);label.text=caption;label.gameObject.AddComponent<LayoutElement>().preferredHeight=height;return label;
        }
        private Text BoundLabel(Transform parent,Func<string> value,List<Action> refresh,int height=64)
        {var label=RowLabel(parent,value(),height);refresh.Add(()=>label.text=value());return label;}
        private Button RowButton(Transform parent,string key,string caption,Action action,int height=42)
        {var b=ButtonAt(key,caption,parent,0,0,100,height,action);b.gameObject.AddComponent<LayoutElement>().preferredHeight=height;return b;}
        private void BoundButton(Transform parent,string key,Func<string> value,Action action,List<Action> refresh)
        {var b=RowButton(parent,key,value(),action);refresh.Add(()=>b.GetComponentInChildren<Text>().text=value());}
        private void Clear(Transform parent)
        {
            foreach(var key in buttons.Where(x=>x.Value==null||x.Value.transform.IsChildOf(parent)).Select(x=>x.Key).ToArray())
                buttons.Remove(key);
            for(var i=parent.childCount-1;i>=0;i--)
            {
                var child=parent.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }

        private void ToggleTab(string tab) => OpenTab(openTab==tab ? string.Empty : tab);

        public void OpenTab(string tab)
        {
            if (tab == "development" && !developmentUi) return;
            architectCategory = tab=="build" ? "structure" : tab=="zone" ? "zones" : null;
            openTab = tab=="build" || tab=="zone" ? "architect" : tab;
            panel.gameObject.SetActive(openTab.Length>0);
            UpdateInspectorVisibility();
            Clear(panelContent);
            panelRefreshers.Clear();
            architectDescription = null;
            CancelTool();
            commandScroll.StopMovement();
            panelContent.anchoredPosition = Vector2.zero;
            LayoutCommandScroll();
            if(openTab=="architect") BuildArchitect();
            else if(tab=="work")
            {
                RowLabel(panelContent,"작업 배분 · 1 우선 / 3 후순위 / 끔 제외",32);
                var heading=WorkRow(panelContent, 34);
                RowLabel(heading,"정착민",34,16);
                foreach(WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
                    RowLabel(heading,WorkDiagnostics.GetCategoryText(category),34,16).alignment=TextAnchor.MiddleCenter;
                SizeWorkColumns(heading);
                foreach(var pawn in Runner.Session.World.Colonists)
                {
                    var id=pawn.Id;var row=WorkRow(panelContent, 38);RowLabel(row,"정착민 "+id,42,16);
                    foreach(WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
                    {
                        var cat=category;Func<int> priority=()=>Runner.Session.World.WorkPolicies.First(p=>p.PawnId==id).GetPriority(cat);
                        BoundButton(row,"priority."+id+"."+cat,()=>priority()==0?"끔":priority().ToString(),
                            ()=>Runner.SetWorkPriority(id,cat,(priority()+1)%4),panelRefreshers);
                    }
                    SizeWorkColumns(row);
                }
            }
            else if (openTab == "development")
            {
                BoundLabel(panelContent, () => "개발 패널 · Tick " + Runner.Session.World.Tick +
                    "\n경로 탐색 " + Runner.Session.World.PathSearchCount + " · 초기화 " + Runner.Session.ResetCount, panelRefreshers, 60);
                var row = WorkRow(panelContent);
                BoundButton(row, "dev.pause", () => Runner.IsPaused ? "재생" : "일시정지",
                    () => Runner.SetPaused(!Runner.IsPaused), panelRefreshers);
                var step = RowButton(row, "dev.step", "한 Tick 실행", Runner.StepOnce);
                step.interactable = Runner.IsPaused;
                panelRefreshers.Add(() => step.interactable = Runner.IsPaused && !Runner.IsUiBlocked);
                RowButton(row, "dev.reset", "월드 초기화", () =>
                {
                    Runner.ResetWorld();
                    Refresh();
                });
                EqualizeWorkCells(row);
            }
        }

        private void LayoutCommandScroll()
        {
            var horizontal = openTab == "work" && panel.rect.width - 20 < WorkTableMinimumWidth;
            commandScroll.horizontal = horizontal;
            panelContent.anchorMax = new Vector2(horizontal ? 0 : 1, 1);
            panelContent.pivot = new Vector2(0, 1);
            panelContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, horizontal
                ? WorkTableMinimumWidth : Mathf.Max(0, panel.rect.width - 20));
            var position = panelContent.anchoredPosition;
            panelContent.anchoredPosition = new Vector2(horizontal ? position.x : 0, position.y);
        }

        private static void SizeWorkColumns(Transform row)
        {
            for (var index = 0; index < row.childCount; index++)
            {
                var cell = row.GetChild(index).GetComponent<LayoutElement>();
                cell.minWidth = index == 0 ? 122 : 92;
                cell.preferredWidth = cell.minWidth;
                cell.flexibleWidth = 1;
            }
        }

        private bool HasArchitectTool => openTab == "architect" && ActiveTool != Tool.None;

        private void BuildArchitect()
        {
            Clear(panelContent);
            var content = Box("Architect categories and tools", panelContent, Color.clear);
            content.gameObject.AddComponent<LayoutElement>().preferredHeight = 154;
            var index = 0;
            foreach (var category in new[] { ("structure","구조"), ("furniture","가구"), ("production","생산"), ("zones","구역"), ("orders","명령") })
            {
                var key = category.Item1;
                var button = ButtonAt("architect." + key, category.Item2, content, (index % 2) * 98,
                    (index / 2) * 46, 94, 40, () =>
                    {
                        CancelTool();
                        architectCategory = key;
                        BuildArchitect();
                    });
                if (architectCategory == key) button.targetGraphic.color = new Color(.30f,.40f,.26f);
                index++;
            }

            architectDescription = Label("Architect tool description", content, 16);
            var descriptionRect = architectDescription.rectTransform;
            descriptionRect.anchorMin = new Vector2(0, 1);
            descriptionRect.anchorMax = new Vector2(1, 1);
            descriptionRect.offsetMin = new Vector2(202, -154);
            descriptionRect.offsetMax = new Vector2(-4, -82);

            switch (architectCategory)
            {
                case "structure":
                    AddBuildingTool(content, BuildingKind.Wall, 0);
                    AddBuildingTool(content, BuildingKind.Door, 1);
                    break;
                case "furniture":
                    AddBuildingTool(content, BuildingKind.Bed, 0);
                    break;
                case "production":
                    AddBuildingTool(content, BuildingKind.Stove, 0);
                    break;
                case "zones":
                    AddArchitectTool(content, 0, "tool.storage", "저장", "overlay.storage", () => BeginTool(Tool.Storage));
                    AddArchitectTool(content, 1, "tool.growing", "경작", "overlay.growing", () => BeginTool(Tool.Growing));
                    break;
                case "orders":
                    AddArchitectTool(content, 0, "tool.gather", "채집", "tree", () => BeginTool(Tool.Gather));
                    break;
            }
        }

        private void AddBuildingTool(Transform parent, BuildingKind kind, int index)
        {
            AddArchitectTool(parent, index, "build." + kind, ConstructionPanel.Name(kind) + "\n목재 " + ConstructionRules.Cost(kind),
                VisualCatalog.BuildingKey(kind), () => BeginTool(Tool.Building, kind));
        }

        private void AddArchitectTool(Transform parent, int index, string key, string caption, string visualKey, Action action)
        {
            var button = ButtonAt(key, caption, parent, 202 + index * 78, 0, 72, 72, action);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = 14;
            if (visualCatalog == null || !visualCatalog.TryGet(visualKey, out var visual)) return;
            var icon = Box("Tool icon", button.transform, Color.clear);
            Place(icon, 24, 2, 24, 24);
            var image = icon.GetComponent<Image>();
            image.sprite = visual.Sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            Place(text.rectTransform, 2, 27, 68, 45);
        }

        private void UpdateInspectorVisibility()
        {
            var hasSelection = string.IsNullOrEmpty(openTab) && (SelectedPawnId.HasValue || SelectedObjectId.HasValue);
            selectionCard.gameObject.SetActive(hasSelection);
            selectionCommands.gameObject.SetActive(hasSelection);
            inspection.gameObject.SetActive(hasSelection && detailOpen);
        }

        private void LayoutSelection(float width, float height)
        {
            var summaryWidth = Mathf.Min(380, width * .34f);
            var commandX = 16 + summaryWidth;
            var commandWidth = Mathf.Max(66, width - commandX - 236);
            Place(selectionCard, 8, height - 188, summaryWidth, 144);
            Place(selectionCommands, commandX, height - 188, commandWidth, 144);
            var detailHeight = Mathf.Clamp(height - 282, 72, 360);
            Place(inspection, 8, height - 192 - detailHeight, Mathf.Min(540, width - 244), detailHeight);
            var columns = Mathf.Clamp(Mathf.FloorToInt((commandWidth + 6) / 72), 2, 4);
            for (var index = 0; index < selectionCommands.childCount; index++)
                Place((RectTransform)selectionCommands.GetChild(index), index % columns * 72, 144 - 66 - index / columns * 72, 66, 66);
        }

        private Button CommandTile(string key, Func<string> caption, Action action)
        {
            var button = ButtonAt(key, caption(), selectionCommands, 0, 0, 66, 66, action);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = 14;
            var visualKey = SelectionCommandIcon(key);
            if (visualCatalog != null && visualCatalog.TryGet(visualKey, out var visual))
            {
                var icon = Box("Command icon", button.transform, Color.clear);
                Place(icon, 22, 1, 22, 22);
                var image = icon.GetComponent<Image>();
                image.sprite = visual.Sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                Place(text.rectTransform, 2, 24, 62, 42);
            }
            detailRefreshers.Add(() => text.text = caption());
            return button;
        }

        private string SelectionCommandIcon(string key)
        {
            switch (key)
            {
                case "draft": return "icon.draft";
                case "selection.detail":
                    if (SelectedPawnId.HasValue) return "icon.sleep";
                    return Runner.Session.World.Buildings.Any(b => b.Exists && b.Id == SelectedObjectId && b.Kind == BuildingKind.Stove)
                        ? VisualCatalog.BuildingKey(BuildingKind.Stove) : "overlay.storage";
                case "locate": return "overlay.selection";
                case "next.object": return "pawn.front";
                case "gather.toggle": return "tree";
                case "sowing":
                case "delete.growing": return "overlay.growing";
                case "delete.storage": return "overlay.storage";
                default: return "icon.work";
            }
        }

        private void SetSelectionSummary(Func<string> caption)
        {
            selectionSummary.text = caption();
            detailRefreshers.Add(() => selectionSummary.text = caption());
        }

        private void AddDetailToggle(string caption)
        {
            CommandTile("selection.detail", () => detailOpen ? "상세 닫기" : caption, () => SetDetailOpen(!detailOpen));
        }

        private void SetDetailOpen(bool value)
        {
            detailOpen = value;
            UpdateInspectorVisibility();
        }

        private void FocusTarget(GridPosition position)
        {
            Map.Focus(position);
            var point = Map.GridToScreen(position);
            if (inspection.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(inspection, point))
                SetDetailOpen(false);
            if (!MapInput.IsPointerOverUI(point)) return;

            var cameraRect = Map.MapCamera.pixelRect;
            var initial = Map.MapCamera.transform.position;
            // Seek an exposed position without changing the user's zoom or bypassing map limits.
            foreach (var target in new[] { new Vector2(.5f,.55f), new Vector2(.65f,.65f), new Vector2(.35f,.65f),
                new Vector2(.65f,.4f), new Vector2(.35f,.4f) })
            {
                var desired = cameraRect.min + Vector2.Scale(cameraRect.size, target);
                var current = Map.GridToScreen(position);
                Map.Pan((current - desired) * (2 * Map.ZoomSize / Mathf.Max(1, cameraRect.height)));
                if (!MapInput.IsPointerOverUI(Map.GridToScreen(position))) return;
            }
            Map.Pan(initial - Map.MapCamera.transform.position);
        }

        private void AddSelectionNavigation(Func<GridPosition> position)
        {
            CommandTile("locate", () => "위치 보기", () => FocusTarget(position()));
            var next = CommandTile("next.object", () => "같은 칸\n대상", () => SelectNextAt(position()));
            void UpdateAvailability() => next.interactable = SelectionCandidates(position()).Count > 1;
            UpdateAvailability();
            detailRefreshers.Add(UpdateAvailability);
        }

        private RectTransform WorkRow(Transform parent, int height = 44)
        {
            var row=Box("Work row",parent,Color.clear);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;
            return row;
        }

        private static void EqualizeWorkCells(Transform row)
        {
            foreach(Transform cell in row)
            {
                var layout=cell.GetComponent<LayoutElement>();
                layout.minWidth=0;layout.preferredWidth=0;layout.flexibleWidth=1;
            }
        }

        private void BuildInspector(GridPosition? hitCell = null)
        {
            detailOpen = false;
            UpdateInspectorVisibility();
            Clear(inspectContent);
            Clear(selectionCommands);
            detailRefreshers.Clear();
            var world = Runner.Session.World;
            var pawn = world.Colonists.FirstOrDefault(x => x.Id == SelectedPawnId);
            if (pawn != null)
            {
                SetSelectionSummary(() => "정착민 " + pawn.Id + (pawn.IsDrafted ? " · 소집" : "") +
                    "\n행동: " + PawnAction(pawn) + "\n운반: " + (pawn.CargoAmount > 0
                        ? SettlementView.ResourceName(pawn.CargoKind) + " " + pawn.CargoAmount : "없음") +
                    "\n목적지: " + (pawn.Destination?.ToString() ?? "없음"));
                BoundLabel(inspectContent, () => "욕구\n포만도 " + pawn.Nutrition / 100 + "   휴식 " + pawn.Rest / 100, detailRefreshers);
                AddDetailToggle("욕구");
                CommandTile("draft", () => pawn.IsDrafted ? "해제 [R]" : "소집 [R]", () => Runner.ToggleDraft(pawn.Id));
                AddSelectionNavigation(() => pawn.Position);
            }
            else if (SelectedObjectId.HasValue)
            {
                BuildObjectInspector(world, SelectedObjectId.Value, hitCell);
            }
            LayoutSelection(canvasRoot.rect.width, canvasRoot.rect.height);
        }

        private void BuildObjectInspector(WorldState world, int id, GridPosition? hitCell)
        {
            var building = world.Buildings.FirstOrDefault(x => x.Id == id && x.Exists);
            var bed = world.Beds.FirstOrDefault(x => x.Id == id && x.Exists);
            var blueprint = world.Blueprints.FirstOrDefault(x => x.Id == id && x.Exists);
            var node = world.Gatherables.FirstOrDefault(x => x.Id == id && !x.Depleted);
            var stock = world.StorageZones.FirstOrDefault(x => x.Id == id && x.Exists);
            var growing = world.GrowingZones.FirstOrDefault(x => x.Id == id && x.Exists);
            var crop = world.Crops.FirstOrDefault(x => x.Id == id && x.Exists);
            var resource = world.Resources.FirstOrDefault(x => x.Id == id && x.Exists);
            GridPosition position;
            if (building != null)
            {
                position = building.Position;
                SetSelectionSummary(() => ConstructionPanel.Name(building.Kind) + (building.DemolitionDesignated ? " · 철거 지정" : "") +
                    (bed != null ? "\n사용자: " + (bed.OwnerId?.ToString() ?? "없음") : "") +
                    (building.Kind == BuildingKind.Stove ? "\n" + WorkDiagnostics.GetReasonText(world.GetWorkDiagnostic(WorkCategory.Cook,id)) : ""));
                CommandTile("demolish", () => building.DemolitionDesignated ? "철거 취소" : "철거 지정",
                    () => Runner.SetDemolition(id, !building.DemolitionDesignated));
                if (building.Kind == BuildingKind.Stove)
                {
                    AddDetailToggle("조리 주문");
                    BuildCookingInspector(id);
                }
            }
            else if (blueprint != null)
            {
                position = blueprint.Position;
                SetSelectionSummary(() => ConstructionPanel.Name(blueprint.Kind) + " 설계도\n목재 " + blueprint.DeliveredWood + "/" +
                    blueprint.Cost + "  시공 " + blueprint.Progress + "/" + blueprint.Duration + "\n" +
                    WorkDiagnostics.GetReasonText(world.GetWorkDiagnostic(WorkCategory.Build,id)));
                CommandTile("cancel.blueprint", () => "건설 취소", () => { Runner.CancelBlueprint(id); ClearSelection(); });
            }
            else if (node != null)
            {
                position = node.Position;
                SetSelectionSummary(() => (node.Kind == GatherKind.Tree ? "나무" : "채집 식물") + "\n채집 " + node.Progress + "/" + node.Duration);
                CommandTile("gather.toggle", () => node.Designated ? "채집 취소" : "채집 지정",
                    () => Runner.DesignateGather(id, !node.Designated));
            }
            else if (stock != null)
            {
                position = hitCell ?? stock.Slots[0].Position;
                SetSelectionSummary(() => "저장 구역 · " + stock.Slots.Count + "칸\n허용: " +
                    string.Join(", ", Enum.GetValues(typeof(ResourceKind)).Cast<ResourceKind>().Where(stock.Allows).Select(SettlementView.ResourceName)));
                AddDetailToggle("저장 필터");
                foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
                {
                    var filter = kind;
                    BoundButton(inspectContent, "filter." + filter, () => SettlementView.ResourceName(filter) + (stock.Allows(filter) ? " 허용" : " 차단"),
                        () => Runner.SetStorageFilter(id, filter, !stock.Allows(filter)), detailRefreshers);
                }
                CommandTile("delete.storage", () => "구역 삭제", () => { Runner.DeleteStorage(id); ClearSelection(); });
            }
            else if (growing != null)
            {
                position = hitCell ?? growing.Cells[0].Position;
                SetSelectionSummary(() => "경작 구역 · " + growing.Cells.Count + "칸\n" + (growing.SowingEnabled ? "파종 허용" : "파종 중지"));
                CommandTile("sowing", () => growing.SowingEnabled ? "파종 허용\n눌러 중지" : "파종 중지\n눌러 허용",
                    () => Runner.SetSowingEnabled(id, !growing.SowingEnabled));
                CommandTile("delete.growing", () => "구역 삭제", () => { Runner.DeleteGrowingZone(id); ClearSelection(); });
            }
            else if (crop != null)
            {
                position = crop.Position;
                SetSelectionSummary(() => "작물 · " + (crop.Stage == CropStage.Empty ? "파종 대기" : crop.Stage == CropStage.Mature ? "수확 가능" : "성장 중") +
                    "\n성장 " + crop.GrowthTicks + "/2400");
            }
            else if (resource != null)
            {
                position = resource.Position;
                SetSelectionSummary(() => SettlementView.ResourceName(resource.Kind) + " × " + resource.Amount + "\n점유: " +
                    (resource.OwnerId?.ToString() ?? "없음"));
            }
            else if (bed != null)
            {
                position = bed.Position;
                SetSelectionSummary(() => "침대\n사용자: " + (bed.OwnerId?.ToString() ?? "없음"));
            }
            else return;

            AddSelectionNavigation(() => position);
        }

        private void BuildCookingInspector(int stove)
        {
            Func<CookingOrderState> order=()=>Runner.Session.World.CookingOrders.FirstOrDefault(x=>x.Exists&&x.StoveId==stove);
            BoundLabel(inspectContent,()=>{
                var o=order();var output=Runner.Session.World.CookingOutputs.FirstOrDefault(x=>x.Exists&&x.StoveId==stove);
                return o==null?"조리 주문 없음":"목표 "+o.TargetMeals+" · "+(o.Enabled?"가동":"중지")+"\n입력 "+o.InputIngredients+"/2 · 조리 "+o.Progress+"/40 · 출력 "+(output?.Amount??0)+"/50\n"+WorkDiagnostics.GetReasonText(Runner.Session.World.GetWorkDiagnostic(WorkCategory.Cook,stove));
            },detailRefreshers,108);
            BoundButton(inspectContent,"cook.toggle",()=>order()==null?"주문 재생성":order().Enabled?"조리 중지":"조리 가동",()=>{var o=order();Runner.SetCookingOrder(stove,o==null?false:!o.Enabled,o?.TargetMeals??12);},detailRefreshers);
            RowButton(inspectContent,"cook.more","목표 +1",()=>{var o=order();if(o!=null)Runner.SetCookingOrder(stove,o.Enabled,Math.Min(99,o.TargetMeals+1));});
            RowButton(inspectContent,"cook.less","목표 -1",()=>{var o=order();if(o!=null)Runner.SetCookingOrder(stove,o.Enabled,Math.Max(0,o.TargetMeals-1));});
            RowButton(inspectContent,"cook.delete","주문 삭제",()=>Runner.DeleteCookingOrder(stove));
        }
        private static string PawnAction(ColonistState p)
        {
            if(p.IsDrafted)return "소집 · 직접 명령 대기";
            if(p.LifeActivity==LifeActivity.Eating)return "식사";
            if(p.LifeActivity==LifeActivity.SleepingInBed||p.LifeActivity==LifeActivity.SleepingOnFloor)return "수면";
            if(p.LifeActivity==LifeActivity.GoingToFood)return "식사하러 이동";
            if(p.LifeActivity==LifeActivity.GoingToBed)return "침대로 이동";
            if(p.FeatureStep.HasValue)
            {
                switch(p.FeatureStep.Value)
                {
                    case WorkStep.DeliverBuild:return "건설 자재 운반";
                    case WorkStep.Construct:return "시공";
                    case WorkStep.Demolish:return "철거";
                    case WorkStep.Sow:return "파종";
                    case WorkStep.Harvest:return "수확";
                    case WorkStep.DeliverCook:return "조리 재료 운반";
                    case WorkStep.Cook:return "조리";
                }
            }
            switch(p.WorkActivity)
            {
                case WorkActivity.GoingToGather:return "채집하러 이동";
                case WorkActivity.Gathering:return "채집";
                case WorkActivity.GoingToPickup:return "물자 가지러 이동";
                case WorkActivity.PickingUp:return "물자 수거";
                case WorkActivity.GoingToStore:return "저장 구역으로 운반";
                case WorkActivity.Storing:return "물자 보관";
                default:return p.Destination.HasValue?"이동":"대기";
            }
        }
    }
}
