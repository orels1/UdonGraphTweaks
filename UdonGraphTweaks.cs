
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;

namespace UGT {
  public class UdonGraphTweaks : EditorWindow {
    public UdonGraphProgramAsset graphToInspect;
    private static VisualTreeAsset visualTree;
    [MenuItem("Tools/UdonGraphTweaks",  false, 1999)]
    public static void CreateWindow() {
      var window = (UdonGraphTweaks) GetWindow(typeof(UdonGraphTweaks), false, "UGT");
      window.minSize = new Vector2( 200, 400 );
      window.Show();
      keyPressed = false;
      lastPressedKey = KeyCode.None;
    }

    private void OnEnable() {
      if (Application.isPlaying) return; // we do not bind anything in play mode
      var window = (UdonGraphTweaks) GetWindow(typeof(UdonGraphTweaks), false);
      keyPressed = false;
      lastPressedKey = KeyCode.None;

      visualTree =
        AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
          "Assets/Editor/UGT/UdonGraphTweaks.uxml");
      var ui = visualTree.CloneTree((string)null);
      window.rootVisualElement.styleSheets.Add(
        AssetDatabase.LoadAssetAtPath<StyleSheet>(
          "Assets/Editor/UGT/UdonGraphTweaks.uss"));
      BindClicks(ui);
      var delayedBind = rootVisualElement.schedule.Execute(() => {
        BindGraphCallbacks(window.rootVisualElement);
      });
      delayedBind.ExecuteLater(1000);
      window.rootVisualElement.Add(ui);
    }

    private static void BindClicks(VisualElement root) {
      root.Q<Button>("EventStart").clicked += () => {
        CreateSimpleEventNode("Start");
      };
      root.Q<Button>("EventUpdate").clicked += () => {
        CreateSimpleEventNode("Update");
      };
      root.Q<Button>("EventPostLateUpdate").clicked += () => {
        CreateSimpleEventNode("PostLateUpdate");
      };
      root.Q<Button>("EventInteract").clicked += CreateInteractWithLog;
      root.Q<Button>("EventTriggerEnter").clicked += () => {
        CreateTriggerEventWithLog(false, false);
      };
      root.Q<Button>("EventTriggerExit").clicked += () => {
        CreateTriggerEventWithLog(false, true);
      };
      root.Q<Button>("EventPlayerTriggerEnter").clicked += () => {
        CreateTriggerEventWithLog(true, false);
      };
      root.Q<Button>("EventPlayerTriggerExit").clicked += () => {
        CreateTriggerEventWithLog(true, true);
      };
      root.Q<Button>("EventPickup").clicked += () => {
        CreateSimpleEventNode("OnPickup");
      };
      root.Q<Button>("EventDrop").clicked += () => {
        CreateSimpleEventNode("OnDrop");
      };
      root.Q<Button>("ConstString").clicked += CreateStringConst;
      root.Q<Button>("ConstInt").clicked += CreateIntConst;
      root.Q<Button>("ConstFloat").clicked += CreateFloatConst;
      root.Q<Button>("ConstBool").clicked += CreateBoolConst;
      root.Q<Button>("ConstVector2").clicked += () => CreateVector2Const();
      root.Q<Button>("ConstVector3").clicked += () => CreateVector3Const();
      root.Q<Button>("ConstVector4").clicked += () => CreateVector4Const();
      root.Q<Button>("ExtraDebug").clicked += CreateDebugLog;
      root.Q<Button>("ExtraGameObject").clicked += CreateThisConst;
      root.Q<Button>("ExtraTransform").clicked += CreateTransformNode;
      root.Q<Button>("ExtraIterator").clicked += CreateArrayIterator;
    }

    private static KeyCode lastPressedKey;
    private static bool keyPressed;
    private static UdonGraphWindow targetWindow;

    private void OnDestroy() {
      var w = GetUdonGraphWindow(true);
      if (w == null) return;
      var view = GetUdonGraph(w);
      if (view == null) return;
      view.UnregisterCallback<KeyDownEvent>(KeyDownCallback);
      view.UnregisterCallback<KeyUpEvent>(KeyUpCallback);
      view.UnregisterCallback<MouseUpEvent>(MouseUpCallback);
    }

    public static void KeyDownCallback(KeyDownEvent evt) { {
      var w = GetUdonGraphWindow(true);
      var view = GetUdonGraph(w);
      if (evt.target != view) return;
      keyPressed = true;
      if (evt.keyCode == KeyCode.None) return;
      lastPressedKey = evt.keyCode;
      if (lastPressedKey == KeyCode.F && evt.ctrlKey) {
        ShowGraphSearch();
      } 
    }}

    public static void KeyUpCallback(KeyUpEvent evt) { {
      keyPressed = false;
    }}

    public static void MouseUpCallback(MouseUpEvent evt) {
      var w = GetUdonGraphWindow(true);
      var view = GetUdonGraph(w);
      if (evt.target != view) return;
      if (evt.button != 0) return;
      if (!keyPressed) return;
      switch (lastPressedKey) {
        case KeyCode.S: CreateStringConst(); break;
        case KeyCode.I: CreateInteractWithLog(); break;
        case KeyCode.L: CreateDebugLog(); break;
        case KeyCode.B: BreakUpCompositeNode(); break;
        case KeyCode.C: ConvertToPublicVar(); break;
        case KeyCode.Q: CreateBoolConst(); break;
        case KeyCode.T: TestCreate(); break;
        case KeyCode.Alpha0: CreateIntConst(); break;
        case KeyCode.Alpha1: CreateFloatConst(); break;
        case KeyCode.Alpha2: CreateVector2Const(evt.modifiers == EventModifiers.Shift); break;
        case KeyCode.Alpha3: CreateVector3Const(evt.modifiers == EventModifiers.Shift); break;
        case KeyCode.Alpha4: CreateVector4Const(evt.modifiers == EventModifiers.Shift); break;
      }

      keyPressed = false;
      lastPressedKey = KeyCode.None;
    }

    private static void BindGraphCallbacks(VisualElement root) {
      var w = GetUdonGraphWindow(true);
      if (w == null) {
        var delayedCall = root.schedule.Execute(() => {
          BindGraphCallbacks(root);
        });
        delayedCall.ExecuteLater(1000);
        return;
      }

      var view = GetUdonGraph(w);
      if (view == null) {
        var delayedCall = root.schedule.Execute(() => {
          BindGraphCallbacks(root);
        });
        delayedCall.ExecuteLater(1000);
        return;
      }
      view.RegisterCallback<KeyDownEvent>(KeyDownCallback);
      view.RegisterCallback<KeyUpEvent>(KeyUpCallback);
      view.RegisterCallback<MouseUpEvent>(MouseUpCallback);
      view.RegisterCallback<MouseLeaveEvent>(evt => {
        keyPressed = false;
        lastPressedKey = KeyCode.None;
      });
      
      targetWindow = w;
      var checkWindowChange = root.schedule.Execute(CheckWindowChange);
      checkWindowChange.Every(1000);
      checkWindowChange.StartingIn(1000);

      var ugtStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
        "Assets/Editor/UGT/UdonGraphTweaks_GraphTheme.uss");
      var graphThemeToggle = GetWindow<UdonGraphTweaks>().rootVisualElement.Q<Toggle>("UGTGraphTheme");
      graphThemeToggle.RegisterValueChangedCallback(evt => {
        if (evt.newValue) {
          if (!w.rootVisualElement.styleSheets.Contains(ugtStyles)) {
            w.rootVisualElement.styleSheets.Add(ugtStyles);
            w.rootVisualElement.AddToClassList("ugt");
          }
        }
        else {
          if (w.rootVisualElement.styleSheets.Contains(ugtStyles)) {
            w.rootVisualElement.styleSheets.Remove(ugtStyles);
            w.rootVisualElement.RemoveFromClassList("ugt");
          }
        }
      });
      if (graphThemeToggle.value) {
        if (!w.rootVisualElement.styleSheets.Contains(ugtStyles)) {
          w.rootVisualElement.styleSheets.Add(ugtStyles);
          w.rootVisualElement.AddToClassList("ugt");
        }
      }

    }

    public static void CheckWindowChange() {
      if (targetWindow != null) return;
      var w = GetUdonGraphWindow(true);
      if (w == null) return;
      if (!w.Equals(targetWindow)) {
        BindGraphCallbacks(w.rootVisualElement);
      }
    }

    private static void Reload() {
      var w = GetWindow(typeof(UdonGraphTweaks));
      var root = w.rootVisualElement;
      root.Clear();
      visualTree =
        AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
          "Assets/Editor/UGT/UdonGraphTweaks.uxml");
      visualTree.CloneTree(root);
    }
    
    public static UdonGraphWindow GetUdonGraphWindow(bool skipMouseChecks = false) {
      if (!HasOpenInstances<UdonGraphWindow>()) {
        targetWindow = null;
        return null;
      }
      var window = GetWindow<UdonGraphWindow>();
      if (window == null) return null;
      if (skipMouseChecks) {
        return window;
      }

      if (GetWindow<UdonGraphTweaks>().position.Contains(Event.current.mousePosition)) {
        return window;
      }
      if (!window.position.Contains(Event.current.mousePosition)) return null;
      return window;
    }

    public static UdonGraph GetUdonGraph(UdonGraphWindow window) {
      return GetPrivateFieldValue<UdonGraph>("_graphView", window);
    }

    public static UdonGraphProgramAsset GetUdonGraphAsset(UdonGraphWindow window) {
      return GetPrivateFieldValue<UdonGraphProgramAsset>("_graphAsset", window);
    }

    public static T GetPrivateFieldValue<T>(string fieldName, object source) {
      return (T)source.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(source);
    }

    public static void ReloadGraph(UdonGraph view) {
      view.GetType().GetMethod("Reload")?.Invoke(view, new object[]{});
    }

    public static void SaveAndReload(UdonGraphProgramAsset graph, UdonGraphData data, UdonGraph view) {
      Undo.RecordObject(graph, "Updated graph");
      graph.graphData = data;
      ReloadGraph(view);
    }

    public static Vector2 GetGraphMousePosition(UdonGraphWindow window, UdonGraph view) {
      if (!window.position.Contains(Event.current.mousePosition)) {
        return view.contentViewContainer.WorldToLocal(new Vector2(window.position.x + 200, window.position.y + 200));
      }
      var windowRoot = window.rootVisualElement;
      var lastMousePosition = Event.current.mousePosition; //GetPrivateFieldValue<Vector2>("lastMousePosition", view);
      var windowMousePosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent,
        lastMousePosition - window.position.position);
      return view.contentViewContainer.WorldToLocal(windowMousePosition);
    }

    public static UdonNodeData CreateConstNode<T>(string type, T value, UdonGraphData data) {
      var node = data.AddNode($"Const_{type}");
      var stringData = SerializableObjectContainer.Serialize(value, typeof(T));
      node.nodeValues = new[] { stringData };
      return node;
    }

    public static UdonNodeData CreateConstructorNode(string type, object[] values, UdonGraphData data) {
      var node = data.AddNode(type);
      var valuesList = new List<SerializableObjectContainer>();
      foreach (var val in values) {
        var stringData = SerializableObjectContainer.Serialize(val, val.GetType());
        valuesList.Add(stringData);
      }

      node.nodeValues = valuesList.ToArray();
      return node;
    }

    public static UdonNodeData CreateGenericNode<T>(string type, T value, UdonGraphData data) {
      var node = data.AddNode(type);
      var stringData = SerializableObjectContainer.Serialize(value, typeof(T));
      node.nodeValues = new[] { stringData };
      return node;
    }
    
    public static UdonNodeData CreateGenericNode<T>(string type, T[] values, UdonGraphData data) {
      var node = data.AddNode(type);
      var valuesList = new List<SerializableObjectContainer>();
      foreach (var val in values) {
        var stringData = SerializableObjectContainer.Serialize(val, val?.GetType());
        valuesList.Add(stringData);
      }
      node.nodeValues = valuesList.ToArray();
      return node;
    }
    
    public static UdonNodeData GetSelectedNode(UdonGraph view, UdonGraphData data) {
      var graphEl = view.selection[0] as GraphElement;
      if (graphEl == null) {
        Debug.LogWarning("Selection is not a graph element");
        return null;
      }

      var selectedNode = data.FindNode((view.selection[0] as GraphElement).GetUid());
      if (selectedNode == null) {
        Debug.Log("Could not find selected node");
        return null;
      }

      return selectedNode;
    }

    public static void CreateStringConst() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var node = CreateConstNode("SystemString", "", data);
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static void CreateIntConst() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var node = CreateConstNode("SystemInt32", 0, data);
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }

    public static void CreateFloatConst() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var node = CreateConstNode("SystemSingle", 0f, data);
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static void CreateBoolConst() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var node = CreateConstNode("SystemBoolean", false, data);
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static void CreateVector2Const(bool constructor = false) {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      UdonNodeData node;
      if (!constructor) {
        node = CreateConstNode("UnityEngineVector2", Vector2.zero, data);
      }
      else {
        node = CreateConstructorNode("UnityEngineVector2.__ctor__SystemSingle_SystemSingle__UnityEngineVector2",
          new object[] { 0f, 0f }, data);
      }
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static void CreateVector3Const(bool constructor = false) {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      UdonNodeData node;
      if (!constructor) {
        node = CreateConstNode("UnityEngineVector3", Vector2.zero, data);
      }
      else {
        node = CreateConstructorNode("UnityEngineVector3.__ctor__SystemSingle_SystemSingle_SystemSingle__UnityEngineVector3",
          new object[] { 0f, 0f, 0f }, data);
      }
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static void CreateVector4Const(bool constructor = false) {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      UdonNodeData node;
      if (!constructor) {
        node = CreateConstNode("UnityEngineVector4", Vector2.zero, data);
      }
      else {
        node = CreateConstructorNode("UnityEngineVector4.__ctor__SystemSingle_SystemSingle_SystemSingle_SystemSingle__UnityEngineVector4",
          new object[] { 0f, 0f, 0f, 0f }, data);
      }
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }

    public static void CreateThisConst() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var node = CreateConstNode("This", "", data);
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static void CreateTransformNode() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var node = CreateGenericNode("UnityEngineTransform.__get_transform__UnityEngineTransform", (Transform) null, data);
      node.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }
    
    public static UdonNodeData CreateDebugLogNode(UdonGraphData data) {
      var node = data.AddNode("UnityEngineDebug.__Log__SystemObject__SystemVoid");
      var stringData = SerializableObjectContainer.Serialize("", typeof(string));
      node.nodeValues = new[] {
        stringData
      };
      return node;
    }

    public static UdonNodeData LogSelected(UdonGraphData data, UdonNodeData node) {
      var debugNode = CreateDebugLogNode(data);
      debugNode.AddNode(node, 0, 0);
      return debugNode;
    }

    public static void TestCreate() {
      return;
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var forNode = data.AddNode("For");

      var intNode = CreateConstNode("SystemInt32", 1, data);
      intNode.position = new Vector2(graphMousePosition.x - 200, graphMousePosition.y - 80);
      forNode.AddNode(intNode, 1, 0);

      forNode.nodeValues = new[] {
        forNode.nodeValues[0],
        forNode.nodeValues[1],
        SerializableObjectContainer.Serialize(1), 
      };

      forNode.position = graphMousePosition;
      SaveAndReload(graph, data, view);
    }

    public static void CreateDebugLog() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      if (view.selection.Any() && view.selection.Count == 1) {
        var graphEl = view.selection[0] as UdonNode;
        if (graphEl == null) {
          Debug.LogWarning("Selection is not a graph element");
          return;
        }

        var selectedNode = data.FindNode((view.selection[0] as GraphElement).GetUid());
        if (selectedNode == null) {
          Debug.Log("Could not find selected node");
          return;
        }

        if (graphEl.definition.Outputs.Count == 0) {
          Debug.LogWarning("Only nodes that output values can be logged");
          return;
        }
        var debugNode = LogSelected(data, selectedNode);
        debugNode.position = new Vector2(graphMousePosition.x + 350, graphMousePosition.y - 70);
      }
      else {
        var node = CreateDebugLogNode(data);
        node.position = graphMousePosition;
        var debugNode = node;

        node = CreateConstNode("SystemString", "", data);
        node.position = new Vector2(graphMousePosition.x - 250, graphMousePosition.y + 90);

        debugNode.AddNode(node, 0, 0);
      }

      SaveAndReload(graph, data, view);
    }
    
    public static void CreateInteractWithLog() {
      var window = GetUdonGraphWindow(Event.current.type == EventType.MouseUp);
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      if (data.EventNodes.ToList().Any(i => i.fullName == "Event_Interact")) {
        var el = view.graphElements.ToList().Find(i => i.title == "Interact");
        if (el == null) return;
        view.ClearSelection();
        view.AddToSelection(el);
        view.FrameSelection();
        return;
      }
      
      var node = CreateDebugLogNode(data);
      node.position = new Vector2(graphMousePosition.x + 150, graphMousePosition.y - 60);
      var debugNode = node;

      node = CreateConstNode("SystemString", "Interact!", data);
      node.position = new Vector2(graphMousePosition.x - 150, graphMousePosition.y + 80);
      debugNode.AddNode(node, 0, 0);

      node = data.AddNode("Event_Interact");
      node.position = new Vector2(graphMousePosition.x - 150, graphMousePosition.y - 60);
      node.AddFlowNode(debugNode, 0);

      SaveAndReload(graph, data, view);
    }

    public static void CreateSimpleEventNode(string eventName) {
      var window = GetUdonGraphWindow(Event.current.type == EventType.MouseUp);
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      if (data.EventNodes.ToList().Any(i => i.fullName == $"Event_{eventName}")) {
        var el = view.graphElements.ToList().Find(i => i.title == eventName);
        if (el == null) return;
        view.ClearSelection();
        view.AddToSelection(el);
        view.FrameSelection();
        return;
      }
      
      var node = CreateDebugLogNode(data);
      node.position = new Vector2(graphMousePosition.x + 150, graphMousePosition.y - 60);
      var debugNode = node;

      node = CreateConstNode("SystemString", $"{eventName}!", data);
      node.position = new Vector2(graphMousePosition.x - 150, graphMousePosition.y + 80);
      debugNode.AddNode(node, 0, 0);

      node = data.AddNode($"Event_{eventName}");
      node.position = new Vector2(graphMousePosition.x - 150, graphMousePosition.y - 60);
      node.AddFlowNode(debugNode, 0);

      SaveAndReload(graph, data, view);
    }

    public static void ConvertToPublicVar() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var data = graph.GetGraphData();

      if (!view.selection.Any() || view.selection.Count > 1) {
        return;
      }

      var graphEl = view.selection[0] as GraphElement;
      if (graphEl == null) {
        Debug.LogWarning("Selection is not a graph element");
        return;
      }

      var selectedNode = data.FindNode((view.selection[0] as GraphElement).GetUid());
      if (selectedNode == null) {
        Debug.Log("Could not find selected node");
        return;
      }

      if (!selectedNode.fullName.Contains("Const_")) {
        Debug.LogWarning("Only const nodes can be converted");
        return;
      }

      var blackboard = GetPrivateFieldValue<UdonVariablesBlackboard>("_blackboard", view);
      var node = data.AddNode(selectedNode.fullName.Replace("Const", "Variable"));
      var varName = "newVariable";
      if (view.GetVariableNames.Contains(varName)) {
        varName = view.GetUnusedVariableNameLike(varName);
      }
      var nodeValues = new List<SerializableObjectContainer> {
        selectedNode.nodeValues[0],
        // name
        SerializableObjectContainer.Serialize(varName, typeof(string)),
        // public
        SerializableObjectContainer.Serialize(Event.current.modifiers == EventModifiers.Shift, typeof(bool)),
        // synced
        SerializableObjectContainer.Serialize(false, typeof(bool)),
        // syncType
        SerializableObjectContainer.Serialize("none", typeof(string))
      };
      // make Udon happy
      node.nodeUIDs = new string[] { null, null, null, null, null };
      node.nodeValues = nodeValues.ToArray();
      blackboard.AddFromData(node);
      view.RefreshVariables();
      var varNode = view.MakeVariableNode(node.uid, selectedNode.position, UdonGraph.VariableNodeType.Getter);
      foreach (var n in data.nodes) {
        var dataIndex = n.DataNodes.ToList().IndexOf(selectedNode);
        if (dataIndex != -1) {
          n.RemoveNode(dataIndex);
          n.AddNode(varNode.data, dataIndex, 0);
        }
      }
      data.RemoveNode(selectedNode);
      SaveAndReload(graph, data, view);
    }

    public static void BreakUpCompositeNode() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var data = graph.GetGraphData();

      if (!view.selection.Any() || view.selection.Count > 1) return;
      var selectedNode = GetSelectedNode(view, data);
      if (selectedNode == null) return;

      var nodeType = (view.GetNodeByGuid(selectedNode.uid) as UdonNode)?.definition.Outputs[0].type;
      
      if (nodeType == typeof(Vector2)) {
        var xNode = CreateGenericNode("UnityEngineVector2.__get_x__SystemSingle", Vector3.zero, data);
        xNode.position = new Vector2(selectedNode.position.x + 250, selectedNode.position.y - 40);
        var yNode = CreateGenericNode("UnityEngineVector2.__get_y__SystemSingle", Vector3.zero, data);
        yNode.position = new Vector2(selectedNode.position.x + 250, selectedNode.position.y + 40);
        xNode.AddNode(selectedNode, 0, 0);
        yNode.AddNode(selectedNode, 0, 0);
      }
      else if (nodeType == typeof(Vector3)) {
        var xNode = CreateGenericNode("UnityEngineVector3.__get_x__SystemSingle", Vector3.zero, data);
        xNode.position = new Vector2(selectedNode.position.x + 300, selectedNode.position.y - 80);
        var yNode = CreateGenericNode("UnityEngineVector3.__get_y__SystemSingle", Vector3.zero, data);
        yNode.position = new Vector2(selectedNode.position.x + 300, selectedNode.position.y);
        var zNode = CreateGenericNode("UnityEngineVector3.__get_z__SystemSingle", Vector3.zero, data);
        zNode.position = new Vector2(selectedNode.position.x + 300, selectedNode.position.y + 80);
        xNode.AddNode(selectedNode, 0, 0);
        yNode.AddNode(selectedNode, 0, 0);
        zNode.AddNode(selectedNode, 0, 0);
      }
      else if (nodeType == typeof(Vector3)) {
        var xNode = CreateGenericNode("UnityEngineVector3.__get_x__SystemSingle", Vector3.zero, data);
        xNode.position = new Vector2(selectedNode.position.x + 350, selectedNode.position.y - 120);
        var yNode = CreateGenericNode("UnityEngineVector3.__get_y__SystemSingle", Vector3.zero, data);
        yNode.position = new Vector2(selectedNode.position.x + 350, selectedNode.position.y - 40);
        var zNode = CreateGenericNode("UnityEngineVector3.__get_z__SystemSingle", Vector3.zero, data);
        zNode.position = new Vector2(selectedNode.position.x + 350, selectedNode.position.y + 40);
        var wNode = CreateGenericNode("UnityEngineVector3.__get_z__SystemSingle", Vector3.zero, data);
        wNode.position = new Vector2(selectedNode.position.x + 350, selectedNode.position.y + 120);
        xNode.AddNode(selectedNode, 0, 0);
        yNode.AddNode(selectedNode, 0, 0);
        zNode.AddNode(selectedNode, 0, 0);
        wNode.AddNode(selectedNode, 0, 0);
      }

      SaveAndReload(graph, data, view);
    }

    public static void CreateTriggerEventWithLog(bool playerTrigger, bool exit) {
      var window = GetUdonGraphWindow(Event.current.type == EventType.MouseUp);
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var graphMousePosition = GetGraphMousePosition(window, view);
      var data = graph.GetGraphData();

      var eventName = "";
      eventName += playerTrigger ? "OnPlayer" : "On";
      eventName += "Trigger";
      eventName += exit ? "Exit" : "Enter";
      
      if (data.EventNodes.ToList().Any(i => i.fullName == ("Event_" + eventName))) {
        var el = view.graphElements.ToList().Find(i => i.title == eventName);
        if (el == null) return;
        view.ClearSelection();
        view.AddToSelection(el);
        view.FrameSelection();
        return;
      }

      var nullNode = CreateGenericNode("Const_Null", (object)null, data);

      var equalityNode =
        CreateGenericNode("SystemObject.__op_Inequality__SystemObject_SystemObject__SystemBoolean",
          new object[] { "", "" }, data);

      var branchNode = CreateGenericNode("Branch", true, data);

      var blockNode = data.AddNode("Block");
      
      var debugNode = CreateDebugLogNode(data);

      var stringNode = CreateConstNode("SystemString", (playerTrigger ? "Player" : "") + (exit ? " Exited!" : " Entered!"), data);

      var eventNode = data.AddNode("Event_" + eventName);

      eventNode.AddFlowNode(branchNode, 0);
      branchNode.AddFlowNode(blockNode, 0);
      equalityNode.AddNode(nullNode, 0, 0);
      equalityNode.AddNode(eventNode, 1, 0);
      branchNode.AddNode(equalityNode, 0, 0);
      blockNode.AddFlowNode(debugNode, 0);
      debugNode.AddNode(stringNode, 0, 0);

      eventNode.position = graphMousePosition;
      nullNode.position = new Vector2(graphMousePosition.x, graphMousePosition.y + 120);
      equalityNode.position = new Vector2(graphMousePosition.x + 300, graphMousePosition.y + 120);
      branchNode.position =  new Vector2(graphMousePosition.x + 500, graphMousePosition.y);
      blockNode.position = new Vector2(graphMousePosition.x + 650, graphMousePosition.y);
      debugNode.position = new Vector2(graphMousePosition.x + 750, graphMousePosition.y);
      stringNode.position = new Vector2(graphMousePosition.x + 500, graphMousePosition.y + 140);

      SaveAndReload(graph, data, view);
      view = GetUdonGraph(window);
      var delayedCall = window.rootVisualElement.schedule.Execute(() => {
        var createdUids = new[] {
          eventNode.uid, branchNode.uid, equalityNode.uid, nullNode.uid, blockNode.uid, debugNode.uid, stringNode.uid
        };

        var createdNodes = view.graphElements.ToList().Where(i => createdUids.Contains(i.GetUid())).ToArray();
        view.ClearSelection();
        foreach (var n in createdNodes) {
          view.AddToSelection(n);
        }

        view.FrameSelection();
      });
      delayedCall.ExecuteLater(250);

    }

    public static void CreateArrayIterator() {
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var data = graph.GetGraphData();

      if (!view.selection.Any() || view.selection.Count > 1) return;
      var selectedNode = GetSelectedNode(view, data);
      if (selectedNode == null) return;

      var nodeType = (view.GetNodeByGuid(selectedNode.uid) as UdonNode)?.portsOut?[0]?.portType;

      if (nodeType == null || !nodeType.IsArray) return;
      var baseTypeName = nodeType.GetElementType().FullName.Split('_').FirstOrDefault().Replace(".", "");
      var getterName = baseTypeName + "Array" +
                       ".__Get__SystemInt32__" + baseTypeName;

      var lengthNode = CreateGenericNode(baseTypeName + "Array.__get_Length__SystemInt32", (object) null, data);

      var forNode = data.AddNode("For");
      var getNode = CreateGenericNode(getterName, new object[] { null, 0 }, data);

      var debugNode = CreateDebugLogNode(data);

      var flowIn = CreateGenericNode("Block", new object[] { }, data);
      
      var flowOut = CreateGenericNode("Block", new object[] { }, data);
      
      // connect nodes
      lengthNode.AddNode(selectedNode, 0, 0);
      forNode.AddNode(lengthNode, 1, 0);
      getNode.AddNode(selectedNode, 0, 0);
      getNode.AddNode(forNode, 1, 0);
      debugNode.AddNode(getNode, 0, 0);
      
      flowIn.AddFlowNode(forNode, 0);
      forNode.AddFlowNode(flowOut, 1);
      forNode.AddFlowNode(debugNode, 0);
      
      // apparently some nodes lose built in variables after doing `AddNode`
      forNode.nodeValues = new [] {
        SerializableObjectContainer.Serialize(0),
        SerializableObjectContainer.Serialize(1),
        SerializableObjectContainer.Serialize(1)
      };
      
      // positionNodes;
      var selPos = selectedNode.position;
      flowIn.position = new Vector2(selPos.x, selPos.y - 150);
      lengthNode.position = new Vector2(selPos.x + 220, selPos.y - 40);
      forNode.position = new Vector2(selPos.x + 400, selPos.y - 150);
      getNode.position = new Vector2(selPos.x + 670, selPos.y);
      debugNode.position = new Vector2(selPos.x + 900, selPos.y - 170);
      flowOut.position = new Vector2(selPos.x + 940, selPos.y - 290);
      
      SaveAndReload(graph, data, view);
      view = GetUdonGraph(window);
      var delayedCall = window.rootVisualElement.schedule.Execute(() => {
        var createdUids = new[] {
          flowIn.uid, lengthNode.uid, forNode.uid, getNode.uid, debugNode.uid, flowOut.uid
        };

        var createdNodes = view.graphElements.ToList().Where(i => createdUids.Contains(i.GetUid())).ToArray();
        view.ClearSelection();
        foreach (var n in createdNodes) {
          view.AddToSelection(n);
        }

        view.FrameSelection();
      });
      delayedCall.ExecuteLater(250);
    }

    public static bool searchShown;
    private static List<GraphElement> searchResults;
    private static Label resultCount;
    private static int focusedResult = 0;
    public static void ShowGraphSearch() {
      if (searchShown) return;
      var window = GetUdonGraphWindow();
      if (window == null) return;

      var view = GetUdonGraph(window);
      var graph = GetUdonGraphAsset(window);
      if (view == null || graph == null) return;
      
      var data = graph.GetGraphData();

      var root = window.rootVisualElement;
      
      var ugtStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
        "Assets/Editor/UGT/UdonGraphTweaks_Search.uss");
      if (!root.styleSheets.Contains(ugtStyles)) {
        root.styleSheets.Add(ugtStyles);
      }
      
      var searchContainer = new VisualElement();
      searchContainer.AddToClassList("ugt__searchContainer");
      var searchTextField = new TextField();
      searchTextField.AddToClassList("ugt__searchField");
      searchTextField.RegisterValueChangedCallback(evt => {
        if (evt.newValue.Length > 2) {
          PerformSearch(data, view, evt.newValue.ToLower());
        }
      });
      
      resultCount = new Label();
      resultCount.AddToClassList("ugt__resultCount");
      searchTextField.Add(resultCount);
      
      searchTextField.RegisterCallback<KeyDownEvent>(evt => {
        if (evt.keyCode == KeyCode.Return) {
          NextSearchResult(view);
        }

        if (evt.keyCode == KeyCode.Escape) {
          if (!root.Contains(searchContainer)) return;
          root.Remove(searchContainer);
          searchShown = false;
          if (searchResults != null) {
            searchResults.Clear();
          }
          resultCount.text = "";
          focusedResult = 0;
        }
      });

      
      searchContainer.Add(searchTextField);
      var closeButton = new Button();
      closeButton.text = "X";
      closeButton.AddToClassList("ugt__searchClose");
      closeButton.clicked += () => {
        root.Remove(searchContainer);
        searchShown = false;
      };
      searchContainer.Add(closeButton);
      root.Add(searchContainer);
      searchTextField.Q<TextInputBaseField<string>>().Focus();
    }

    public static void PerformSearch(UdonGraphData data, UdonGraph view, string term) {
      var options = view.graphElements.ToList().Where(i => {
        if (i is UdonNode udonNode) {
          if (udonNode.definition.fullName.Split(' ').FirstOrDefault().Split('_').FirstOrDefault().ToLower()
            .Contains(term)) {
            return true;
          }    
        }
        return i.title.ToLower().Contains(term);
      }).ToArray();
      if (options.Length == 0) return;
      if (resultCount != null) {
        resultCount.text = $"{options.Length}";
      }


      searchResults = new List<GraphElement>();
      view.ClearSelection();
      foreach (var option in options) {
        // var el = view.graphElements.ToList().Find(i => i.GetUid() == option.uid);
        searchResults.Add(option);
        view.AddToSelection(option);
      }
      view.FrameSelection();
    }

    public static void NextSearchResult(UdonGraph view) {
      if (searchResults == null || searchResults.Count == 0) return;
      focusedResult += 1;
      if (focusedResult > searchResults.Count) {
        focusedResult = 1;
      }
      if (resultCount != null) {
        resultCount.text = $"{focusedResult}/{searchResults.Count}";
      }
      view.ClearSelection();
      view.AddToSelection(searchResults[focusedResult - 1]);
      view.FrameSelection();
    }
  }
}

