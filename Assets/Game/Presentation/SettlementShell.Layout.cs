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
        private Text cookingHeader;
        private ScrollRect portraitScroll, commandScroll, inspectorScroll;
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
            inspectorScroll = inspectContent.GetComponentInParent<ScrollRect>();
            cookingHeader = Label("Selected cooking bill", inspection, 15);
            cookingHeader.rectTransform.anchorMin = new Vector2(0, 1);
            cookingHeader.rectTransform.anchorMax = Vector2.one;
            cookingHeader.rectTransform.offsetMin = new Vector2(12, -42);
            cookingHeader.rectTransform.offsetMax = new Vector2(-12, -6);
            cookingHeader.gameObject.SetActive(false);
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
                    SelectPawn(id,ShiftHeld);
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
                BuildPortrait(button.transform, id);
                var nutrition=Box("Nutrition",button.transform,new Color(.20f,.65f,.34f));
                var rest=Box("Rest",button.transform,new Color(.30f,.55f,.85f));
                nutrition.GetComponent<Image>().raycastTarget=false;
                rest.GetComponent<Image>().raycastTarget=false;
                refreshers.Add(() =>
                {
                    Place(nutrition,4,42,54*Mathf.Clamp01(pawn.Nutrition/10000f),3);
                    Place(rest,4,46,54*Mathf.Clamp01(pawn.Rest/10000f),3);
                    nutrition.GetComponent<Image>().color=pawn.Nutrition<=3000?new Color(.95f,.34f,.23f):new Color(.20f,.65f,.34f);
                    rest.GetComponent<Image>().color=pawn.Rest<=2000?new Color(.95f,.66f,.20f):new Color(.30f,.55f,.85f);
                    truncatedPortraits[(RectTransform)button.transform]=fullName+"\n"+PawnAction(pawn)+
                        "\n포만도 "+pawn.Nutrition/100+" · 휴식 "+pawn.Rest/100;
                    button.GetComponent<Image>().color = selectedPawns.Contains(id)
                        ? ActiveColor
                        : pawn.IsDrafted ? new Color(.35f,.41f,.46f) : ControlColor;
                });
                index++;
            }

            Place(colonistBar, 0, 0, Mathf.Max(0, index * PortraitStride - 8), PortraitHeight);
            LayoutPortraits(canvasRoot.rect.width);
        }

        private void BuildPortrait(Transform parent, int pawnId)
        {
            if (visualCatalog == null) return;
            var portrait = Box("Portrait", parent, Color.clear);
            Place(portrait, 7, 2, 48, 48);
            var baseImage = portrait.GetComponent<Image>();
            baseImage.raycastTarget = false;
            if (PawnAppearance.ForId(pawnId).TryResolve(visualCatalog, "front", out var layers))
            {
                baseImage.enabled = false;
                foreach (var layer in layers)
                {
                    var part = Box(layer.Key, portrait, layer.Tint);
                    Stretch(part);
                    var image = part.GetComponent<Image>();
                    image.sprite = layer.Sprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }
            else if (visualCatalog.TryGet("pawn.front", out var fallback))
            {
                baseImage.sprite = fallback.Sprite;
                baseImage.color = WorldMapView.PawnColor(pawnId) * fallback.Tint;
                baseImage.preserveAspect = true;
            }
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
                        Place(portraitTooltip, x, 84, width, 84);
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
            var bottom = CommandPanelHeight(height);
            var occupiedHeight = panel.gameObject.activeSelf ? bottom
                : inspection.gameObject.activeSelf ? 192 + Mathf.Min(360, height - 282)
                : selectionCard.gameObject.activeSelf ? 188 : 44;
            var messageWidth = Mathf.Min(660, width - 244);
            Place(messages, (width - 236 - messageWidth) / 2, height - occupiedHeight - 40, messageWidth, 36);
            messages.gameObject.SetActive(!IsMenuOpen && !string.IsNullOrEmpty(Feedback) && !HasArchitectTool);
            if (size == layoutSize && scale == layoutScale) return;
            layoutSize = size;
            layoutScale = scale;
            var resourceHeight=resourceTotals.Count*26;
            Place(resources, 8, 8, 136, resourceHeight);
            Place(resourceTooltip, 8, 12+resourceHeight, 272, 88);
            LayoutPortraits(width);
            Place(timeControls, width - 184, height - 106, 176, 62);
            var alertHeight = Mathf.Clamp(height - 204, 72, 180);
            Place(alerts, width - 228, height - 118 - alertHeight, 220, alertHeight);
            Place(tabs, 0, height - 40, width, 40);
            tabs.GetComponent<Image>().raycastTarget = false;
            Place((RectTransform)buttons["tab.menu"].transform, width - 120, 0, 120, 40);
            LayoutSelection(width, height);
            LayoutCommandPanel();
            LayoutCommandScroll();
            Map?.SetViewport(new Rect(0,0,1,1));
            LayoutMenu();
        }

        private RectTransform Box(string name,Transform parent,Color? color=null)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            var image=r.gameObject.AddComponent<Image>();image.color=color??PanelColor;image.raycastTarget=color!=Color.clear;
            if (!color.HasValue) ApplySurface(image);
            return r;
        }
        private void ApplySurface(Image image)
        {
            if (visualCatalog == null || !visualCatalog.TryGet("ui.panel", out var panelVisual)) return;
            image.sprite = panelVisual.Sprite;
            image.type = Image.Type.Sliced;
        }

        private static void SetButtonActive(Button button, bool active)
        {
            var surface = active ? ActiveColor : ControlColor;
            if (button.targetGraphic.color == surface) return;
            button.targetGraphic.color = surface;
            var caption = button.GetComponentInChildren<Text>();
            caption.color = active ? Accent : TextColor;
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
            var r=Box(key,parent,ControlColor);Place(r,x,y,w,h);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            ApplySurface((Image)b.targetGraphic);
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, .93f);
            colors.selectedColor = new Color(.78f, .90f, 1f);
            colors.pressedColor = new Color(.72f, .74f, .68f);
            colors.disabledColor = new Color(.68f, .68f, .68f);
            b.colors = colors;
            var text=Label("Caption",r,17);text.text=caption;text.alignment=TextAnchor.MiddleCenter;Stretch(text.rectTransform,4);
            b.onClick.AddListener(()=>{if(!Runner.SaveBusy)action();});buttons[key]=b;return b;
        }
        private RectTransform Scroll(string name,RectTransform parent)
        {
            var outer=Box(name,parent,Color.clear);Stretch(outer,10);
            var viewport=Box("Viewport",outer,new Color(.12f,.12f,.11f));Stretch(viewport);
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
            if (Runner != null && Runner.DeveloperMode != (tab == "development")) Runner.ToggleDeveloperMode();
            var wasArchitect = openTab == "architect";
            architectCategory = tab=="build" ? "structure" : tab=="zone" ? "zones" : null;
            openTab = tab=="build" || tab=="zone" ? "architect" : tab;
            SetButtonActive(buttons["tab.build"], openTab == "architect");
            SetButtonActive(buttons["tab.work"], openTab == "work");
            panel.gameObject.SetActive(openTab.Length>0);
            UpdateInspectorVisibility();
            Clear(panelContent);
            panelRefreshers.Clear();
            architectDescription = null;
            CancelTool();
            commandScroll.StopMovement();
            panelContent.anchoredPosition = Vector2.zero;
            if (wasArchitect != (openTab == "architect")) LayoutCommandPanel();
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
                        var button=buttons["priority."+id+"."+cat];
                        var trigger=button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                        var entry=new UnityEngine.EventSystems.EventTrigger.Entry
                        {eventID=UnityEngine.EventSystems.EventTriggerType.PointerClick};
                        entry.callback.AddListener(data=>{
                            if(((UnityEngine.EventSystems.PointerEventData)data).button==UnityEngine.EventSystems.PointerEventData.InputButton.Right&&!IsInputBlocked)
                                Runner.SetWorkPriority(id,cat,(priority()+3)%4);
                        });
                        trigger.triggers.Add(entry);
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
                var raid = RowButton(row, "dev.raid", "일반 습격 발생", () =>
                {
                    var started = Runner.StartDebugRaid();
                    Report(started, "동쪽에서 일반 습격이 시작됐어.");
                    if (started) Map.Focus(Runner.Session.World.RaidEntrance(0));
                });
                panelRefreshers.Add(() => raid.interactable = Runner.DeveloperMode &&
                    (Runner.Session.World.RaidPhase == RaidPhase.Idle || Runner.Session.World.RaidPhase == RaidPhase.Resolved));
                EqualizeWorkCells(row);
            }
        }

        private float CommandPanelHeight(float height) =>
            Mathf.Min(openTab == "architect" ? 218 : 320, height * .39f);

        private void LayoutCommandPanel()
        {
            var size = canvasRoot.rect.size;
            var height = CommandPanelHeight(size.y);
            var width = openTab == "architect" ? 428 : 880;
            Place(panel, 8, size.y - height, Mathf.Min(width, size.x - 244), height - 44);
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
            panelRefreshers.Clear();
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
                if (architectCategory == key) SetButtonActive(button, true);
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
                    AddArchitectTool(content, 0, "tool.storage", "저장", "overlay.storage", Tool.Storage);
                    AddArchitectTool(content, 1, "tool.growing", "경작", "overlay.growing", Tool.Growing);
                    break;
                case "orders":
                    AddArchitectTool(content, 0, "tool.gather", "채집", "tree", Tool.Gather);
                    break;
            }
        }

        private void AddBuildingTool(Transform parent, BuildingKind kind, int index)
        {
            AddArchitectTool(parent, index, "build." + kind, ConstructionPanel.Name(kind) + "\n목재 " + ConstructionRules.Cost(kind),
                VisualCatalog.BuildingKey(kind), Tool.Building, kind);
        }

        private void AddArchitectTool(Transform parent, int index, string key, string caption, string visualKey,
            Tool tool, BuildingKind kind = BuildingKind.Wall)
        {
            var button = ButtonAt(key, caption, parent, 202 + index * 78, 0, 72, 72, () => BeginTool(tool, kind));
            panelRefreshers.Add(() => SetButtonActive(button, ActiveTool == tool && (tool != Tool.Building || kind == buildingKind)));
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
            var coveredBefore=inspection.gameObject.activeInHierarchy&&
                RectTransformUtility.RectangleContainsScreenPoint(inspection,Map.GridToScreen(position));
            Map.Focus(position);
            var point = Map.GridToScreen(position);
            if (coveredBefore || (inspection.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(inspection, point)))
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
            cookingHeader.gameObject.SetActive(false);
            var outer = (RectTransform)inspectorScroll.transform;
            outer.offsetMax = new Vector2(-10, -10);
            Clear(selectionCommands);
            detailRefreshers.Clear();
            var world = Runner.Session.World;
            var pawn = world.Colonists.FirstOrDefault(x => x.Id == SelectedPawnId);
            if(selectedPawns.Count>1)
            {
                SetSelectionSummary(()=>"정착민 "+selectedPawns.Count+"명 선택\n"+
                    string.Join(", ",selectedPawns.OrderBy(x=>x).Select(x=>"#"+x))+"\n우클릭 이동 · R 소집/해제");
                CommandTile("draft",()=>world.Colonists.Any(p=>selectedPawns.Contains(p.Id)&&!p.IsDrafted)?"소집 [R]":"해제 [R]",ToggleSelectedDraft);
                CommandTile("locate",()=>"위치 보기",()=>FocusTarget(pawn.Position));
                LayoutSelection(canvasRoot.rect.width,canvasRoot.rect.height);
                return;
            }
            if (pawn != null)
            {
                SetSelectionSummary(() => "정착민 " + pawn.Id + (pawn.IsDrafted ? " · 소집" : "") +
                    (world.EquipmentEnabled ? " · "+WeaponName(world.EquippedWeapon(pawn.Id)) : "") + " · 체력 " + pawn.Health + "/100" + (pawn.IsDowned ? " · 전투 불능" : "") +
                    (pawn.Attack.TargetId.HasValue ? " · 표적 #" + pawn.Attack.TargetId : "") + "\n행동: " + DirectAction(world,pawn) + "\n운반: " + (pawn.CargoAmount > 0
                        ? SettlementView.ResourceName(pawn.CargoKind) + " " + pawn.CargoAmount : "없음") +
                    "\n목적지: " + (pawn.Destination?.ToString() ?? "없음"));
                BoundLabel(inspectContent, () => "욕구\n포만도 " + pawn.Nutrition / 100 + "   휴식 " + pawn.Rest / 100, detailRefreshers);
                AddDetailToggle("욕구");
                CommandTile("draft", () => pawn.IsDrafted ? "해제 [R]" : "소집 [R]", () => Runner.ToggleDraft(pawn.Id));
                var cancel=CommandTile("direct.cancel",()=>"지시 취소",()=>ReportDirect(Runner.CancelDirectOrder(pawn.Id),"개인 지시 취소"));
                void UpdateCancel() => cancel.gameObject.SetActive(world.HasEquipOrder(pawn.Id)||world.HasRescueCommand(pawn.Id)||world.PriorityWorkTarget(pawn.Id).HasValue);
                UpdateCancel();detailRefreshers.Add(UpdateCancel);
                AddSelectionNavigation(() => pawn.Position);
            }
            else if (SelectedObjectId.HasValue)
            {
                BuildObjectInspector(world, SelectedObjectId.Value, hitCell);
            }
            LayoutSelection(canvasRoot.rect.width, canvasRoot.rect.height);
        }

        private string DirectAction(WorldState world,ColonistState pawn)
        {
            if(world.HasEquipOrder(pawn.Id))return "무기 가지러 이동";
            var rescue=world.RescueFor(pawn.Id);
            if(rescue!=null)return (rescue.Carrying?"침대로 운반 중 #":"구조하러 이동 #")+rescue.PatientId;
            if(world.RescuerFor(pawn.Id).HasValue)return "구조 중";
            if(world.RescuedBedFor(pawn.Id).HasValue)return "침대에서 치료 대기";
            return (world.PriorityWorkTarget(pawn.Id).HasValue?"우선 · ":"")+PawnAction(pawn);
        }

        private void BuildObjectInspector(WorldState world, int id, GridPosition? hitCell)
        {
            var weapon=world.Weapons.FirstOrDefault(w=>w.Id==id&&!w.HolderId.HasValue);
            if(weapon!=null)
            {
                SetSelectionSummary(()=>WeaponName(weapon)+" · 바닥"+(weapon.ReservedById.HasValue?" · 예약 #"+weapon.ReservedById:"")+"\n사거리 "+weapon.Definition.Range+" · 피해 "+weapon.Definition.Damage);
                AddSelectionNavigation(()=>weapon.Position);
                return;
            }
            var enemy=world.Raiders.FirstOrDefault(x=>x.Id==id);
            if(enemy!=null)
            {
                SetSelectionSummary(()=>"습격자 #"+id+" · 체력 "+enemy.Health+"\n표적 정착민 #"+enemy.Attack.TargetId);
                AddSelectionNavigation(()=>enemy.Position);
                return;
            }
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
                if (building.Durability < 100)
                    CommandTile("repair", () => building.RepairDesignated ? "수리 취소" : "수리 지정",
                        () => Runner.SetRepair(id, !building.RepairDesignated));
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

        private void RebuildCookingInspector()
        {
            BuildInspector();
            detailOpen=true;
            UpdateInspectorVisibility();
        }
        private int? selectedCookingBill;
        private bool cookingConditionsOpen;
        private static string CookingStatus(CookingOrderState bill) =>
            !bill.Enabled?"일시중지":bill.RepeatMode==CookingRepeatMode.RepeatCount&&bill.RemainingCount==0?"횟수 완료":
            WorkDiagnostics.GetReasonText(bill.Reason);
        private static string RepeatName(CookingRepeatMode mode) =>
            mode==CookingRepeatMode.RepeatCount?"횟수 지정":mode==CookingRepeatMode.Forever?"계속 생산":"목표 재고";
        private void BuildCookingInspector(int stove)
        {
            var world=Runner.Session.World;
            var bills=world.CookingOrders.Where(x=>x.Exists&&x.StoveId==stove).OrderBy(x=>x.Priority).ThenBy(x=>x.Id).ToList();
            var selected=bills.FirstOrDefault(x=>x.Id==selectedCookingBill)??bills.FirstOrDefault();
            selectedCookingBill=selected?.Id;
            cookingHeader.gameObject.SetActive(true);
            var scrollFrame = (RectTransform)inspectorScroll.transform;
            scrollFrame.offsetMax = new Vector2(-10, -42);
            detailRefreshers.Add(() => cookingHeader.text = selected == null ? "조리 계획서" :
                $"조리 계획서 #{selected.Id} · {CookingStatus(selected)}");
            RowButton(inspectContent,"cook.add","계획서 추가 · 식사 조리",()=>{
                if (!Runner.AddCookingBill(stove, out var id)) return;
                selectedCookingBill = id;
                RebuildCookingInspector();
                RevealCookingControl("cook.bill."+id);
            },32);
            buttons["cook.add"].GetComponentInChildren<Text>().fontSize=14;
            foreach(var bill in bills)
            {
                var id=bill.Id;
                var chosen=selected!=null&&id==selected.Id;
                var prefix=chosen?"cook.":"cook."+id+".";
                Func<int> quantity=()=>bill.RepeatMode==CookingRepeatMode.RepeatCount?bill.RemainingCount:bill.TargetMeals;
                Action<bool,CookingRepeatMode,int> apply=(enabled,mode,count)=>Runner.ConfigureCookingBill(id,enabled,mode,count,
                    bill.WorkerId,bill.AllowIngredients,bill.IngredientRadius,bill.DeliveryMode,bill.DestinationZoneId);
                BoundButton(inspectContent,"cook.bill."+id,()=>"#"+id+" 식사 · "+
                    (bill.RepeatMode == CookingRepeatMode.Forever ? "계속" :
                        bill.RepeatMode == CookingRepeatMode.TargetStock ? "목표 "+quantity() : "남은 "+quantity())+
                    " · "+CookingStatus(bill),
                    ()=>{selectedCookingBill=id;RebuildCookingInspector();},detailRefreshers);
                buttons["cook.bill."+id].GetComponent<LayoutElement>().preferredHeight=28;
                buttons["cook.bill."+id].GetComponentInChildren<Text>().fontSize=14;
                SetButtonActive(buttons["cook.bill."+id], chosen);
                var controls=WorkRow(inspectContent,28);
                var less = RowButton(controls,prefix+"less","−",()=>apply(bill.Enabled,bill.RepeatMode,Math.Max(0,quantity()-1)),34);
                var more = RowButton(controls,prefix+"more","+",()=>apply(bill.Enabled,bill.RepeatMode,Math.Min(99,quantity()+1)),34);
                detailRefreshers.Add(() => less.interactable = bill.RepeatMode != CookingRepeatMode.Forever && quantity() > 0);
                detailRefreshers.Add(() => more.interactable = bill.RepeatMode != CookingRepeatMode.Forever && quantity() < 99);
                BoundButton(controls,prefix+"mode",()=>bill.RepeatMode==CookingRepeatMode.TargetStock?"재고":bill.RepeatMode==CookingRepeatMode.RepeatCount?"횟수":"계속",
                    ()=>apply(bill.Enabled,(CookingRepeatMode)(((int)bill.RepeatMode+1)%3),quantity()),detailRefreshers);
                BoundButton(controls,prefix+"toggle",()=>bill.Enabled?"중지":"재개",
                    ()=>apply(!bill.Enabled,bill.RepeatMode,quantity()),detailRefreshers);
                RowButton(controls,prefix+"up","↑",()=>{Runner.MoveCookingBill(id,-1);RebuildCookingInspector();},28);
                RowButton(controls,prefix+"down","↓",()=>{Runner.MoveCookingBill(id,1);RebuildCookingInspector();},28);
                RowButton(controls,prefix+"delete","삭제",()=>{Runner.DeleteCookingBill(id);RebuildCookingInspector();},28);
                RowButton(controls,"cook.rowdetails."+id,"상세",()=>{
                    selectedCookingBill=id;cookingConditionsOpen=true;RebuildCookingInspector();
                    RevealCookingControl("cook.details");
                },28);
                foreach(Transform child in controls)
                {
                    child.GetComponent<LayoutElement>().preferredHeight=28;
                    child.GetComponentInChildren<Text>().fontSize=14;
                }
            }
            if(selected==null)
            {
                RowButton(inspectContent,"cook.toggle","주문 재생성",()=>{Runner.SetCookingOrder(stove,false,12);RebuildCookingInspector();});
                return;
            }
            var o=selected;
            // All callbacks submit a complete settings value. Runtime state is only changed by Runner commands.
            Action<bool,CookingRepeatMode,int,int?,bool,int,CookingDeliveryMode,int?> configure=
                (enabled,mode,amount,worker,allow,radius,delivery,zone)=>{
                    Runner.ConfigureCookingBill(o.Id,enabled,mode,amount,worker,allow,radius,delivery,zone);
                };
            Func<int> amount=()=>o.RepeatMode==CookingRepeatMode.RepeatCount?o.RemainingCount:o.TargetMeals;
            BoundLabel(inspectContent,()=>"#"+o.Id+" 식사 조리 · "+RepeatName(o.RepeatMode)+
                (o.RepeatMode == CookingRepeatMode.Forever ? "" : " "+amount())+
                "\n입력 "+o.InputIngredients+"/2 · 조리 "+o.Progress+"/40 · 완성 "+o.CompletedMeals+
                "\n"+CookingStatus(o),detailRefreshers,108);
            RowButton(inspectContent,"cook.details",cookingConditionsOpen?"상세 조건 접기":"상세 조건 · 작업자/재료/운반",()=>{
                cookingConditionsOpen=!cookingConditionsOpen;RebuildCookingInspector();
            });
            if(!cookingConditionsOpen)return;
            BoundButton(inspectContent,"cook.worker",()=>"작업자 · "+(o.WorkerId.HasValue?"정착민 #"+o.WorkerId.Value:"누구나"),()=>{
                var ids=world.Colonists.Select(x=>(int?)x.Id).ToList();ids.Insert(0,null);
                configure(o.Enabled,o.RepeatMode,amount(),ids[(ids.IndexOf(o.WorkerId)+1)%ids.Count],o.AllowIngredients,o.IngredientRadius,o.DeliveryMode,o.DestinationZoneId);
            },detailRefreshers);
            BoundButton(inspectContent,"cook.ingredients",()=>"식재료 · "+(o.AllowIngredients?"허용":"금지"),
                ()=>configure(o.Enabled,o.RepeatMode,amount(),o.WorkerId,!o.AllowIngredients,o.IngredientRadius,o.DeliveryMode,o.DestinationZoneId),detailRefreshers);
            BoundButton(inspectContent,"cook.radius",()=>"재료 탐색 거리 · "+(o.IngredientRadius==999?"무제한":o.IngredientRadius+"칸"),()=>{
                var radii=new[]{0,5,10,20,40,999};var index=Array.IndexOf(radii,o.IngredientRadius);
                configure(o.Enabled,o.RepeatMode,amount(),o.WorkerId,o.AllowIngredients,radii[(index+1)%radii.Length],o.DeliveryMode,o.DestinationZoneId);
            },detailRefreshers);
            BoundButton(inspectContent,"cook.delivery",()=>"완성품 · "+(o.DeliveryMode==CookingDeliveryMode.DropOnFloor?"바닥에 두기":o.DeliveryMode==CookingDeliveryMode.SpecificStorage?"저장 구역 #"+o.DestinationZoneId:"가까운 저장 구역"),()=>{
                var mode=o.DeliveryMode==CookingDeliveryMode.DropOnFloor?CookingDeliveryMode.BestStorage:CookingDeliveryMode.DropOnFloor;
                configure(o.Enabled,o.RepeatMode,amount(),o.WorkerId,o.AllowIngredients,o.IngredientRadius,mode,null);
            },detailRefreshers);
            foreach(var zone in world.StorageZones.Where(x=>x.Exists))
            {
                var zoneId=zone.Id;
                RowButton(inspectContent,"cook.zone."+zoneId,"운반 위치 · 저장 구역 #"+zoneId,
                    ()=>configure(o.Enabled,o.RepeatMode,amount(),o.WorkerId,o.AllowIngredients,o.IngredientRadius,CookingDeliveryMode.SpecificStorage,zoneId));
            }

        }
        private void RevealCookingControl(string key)
        {
            Canvas.ForceUpdateCanvases();
            inspectorScroll.StopMovement();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(inspectContent, buttons[key].transform);
            var limit = Mathf.Max(0, inspectContent.rect.height - inspectorScroll.viewport.rect.height);
            inspectContent.anchoredPosition = new Vector2(0, Mathf.Clamp(-bounds.max.y, 0, limit));
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

