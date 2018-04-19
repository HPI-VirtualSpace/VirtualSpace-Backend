using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY
using UnityEngine;
#endif

namespace VirtualSpace.Shared
{
    [ProtoInclude(500, typeof(FrontendRegistration))]
    [ProtoInclude(501, typeof(FrontendDisconnect))]
    [ProtoInclude(502, typeof(FrontendPayload))]
    [ProtoInclude(503, typeof(FrontendSettings))]
    [ProtoInclude(504, typeof(FrontendLogMessage))]
    [ProtoContract]
    public class FrontendMessage : MessageBase
    {
        
    }

    [ProtoContract]
    public class FrontendRegistration : FrontendMessage
    {
#if BACKEND
        public int SessionId;
#endif
    }

    [ProtoContract]
    public class FrontendDisconnect : FrontendMessage
    {
        
    }

    [ProtoContract]
    public class FrontendLogMessage : FrontendMessage
    {
        [ProtoMember(1)]
        public string Message;
        [ProtoMember(2)]
        public Logger.Level LogLevel;
    }

    [ProtoContract]
    public class FrontendPayload : FrontendMessage
    {
        [ProtoMember(1)]
        public MessageBase Payload;

        private FrontendPayload() { }

        public FrontendPayload(MessageBase payload)
        {
            Payload = payload;
        }
    }
    
    [ProtoInclude(500, typeof(StrategySettings))]
    [ProtoContract]
    public class FrontendSettings : FrontendMessage
    {

    }

    [Serializable]
    [ProtoContract]
    public class StrategySettings : FrontendSettings
    {
        [ProtoMember(1)]
        public StateTransition AllowedTransitions;
        [ProtoMember(2)]
        public bool Run;
        [ProtoMember(3)]
        public bool Reset;
        [ProtoMember(4)]
        public float PreferredSpeed;
        [ProtoMember(5)]
        public float PreferredManeuverSecondDistance;
        [ProtoMember(6)]
        public List<StateTransition> CustomTransitionQueue;
        [ProtoMember(7)]
        public bool PartialUpdate;
        [ProtoMember(8)]
        public int VersionNumber;
        [ProtoMember(9)]
        public double MaxAllowedExecutionSeconds;
        [ProtoMember(10)]
        public double FocusWeight = 2;
        [ProtoMember(11)]
        public int TransitionQueueLength = 1;

        public static StrategySettings Default
        {
            get
            {
                return new StrategySettings() {
                    AllowedTransitions = TransitionHelper.AllStateTransitions & ~StateTransition.AssymmetricRotation 
                        & ~StateTransition.Switch1 & ~StateTransition.Switch2 & ~StateTransition.Switch3 & ~StateTransition.Switch4
                        & ~StateTransition.Focus1 & ~StateTransition.Focus2 & ~StateTransition.Focus3 & ~StateTransition.Focus4,
                    Run = true,
                    PreferredSpeed = -1,
                    PreferredManeuverSecondDistance = -1,
                    CustomTransitionQueue = new List<StateTransition>(),
                    PartialUpdate = false,
                    VersionNumber = 0,
                    MaxAllowedExecutionSeconds = 10f
                };
            }
        }


        public void SetParameters(StrategySettings other)
        {
            if (other.VersionNumber < VersionNumber) return;

            if (!other.PartialUpdate)
            {
                AllowedTransitions = other.AllowedTransitions;
                Reset = other.Reset;
                Run = other.Run;
                PreferredSpeed = other.PreferredSpeed;
                PreferredManeuverSecondDistance = other.PreferredManeuverSecondDistance;
            }

            CustomTransitionQueue = new List<StateTransition>(other.CustomTransitionQueue);
            VersionNumber = other.VersionNumber;
        }

        public StrategySettings() { }

        public StrategySettings(StrategySettings other)
        {
            AllowedTransitions = other.AllowedTransitions;
            Run = other.Run;
            Reset = other.Reset;
            PreferredSpeed = other.PreferredSpeed;
            PreferredManeuverSecondDistance = other.PreferredManeuverSecondDistance;
            PartialUpdate = other.PartialUpdate;
            VersionNumber = other.VersionNumber;
            CustomTransitionQueue = new List<StateTransition>(other.CustomTransitionQueue);
        }

#if UNITY_EDITOR
        private bool shouldFoldout = false;
        public void OnInspectorGUI()
        {
            // make a copy, look if changed
            // version number to compare with Backend?

            GUILayout.Label("General Settings", EditorStyles.boldLabel);
            Run = EditorGUILayout.Toggle("Run", Run);
            Reset = EditorGUILayout.Toggle("Reset", Reset);
            PreferredSpeed = EditorGUILayout.FloatField("Preferred Transition Speed (m/s)", PreferredSpeed);
            PreferredManeuverSecondDistance =
                EditorGUILayout.FloatField("Seconds Between Maneuvers", PreferredManeuverSecondDistance);

            GUILayout.Label("Allowed Transitions", EditorStyles.boldLabel);


            //shouldFoldout = EditorGUILayout.Foldout(shouldFoldout, "Select allowed transitions");

            //if (shouldFoldout)
            //{
            var inspectorWidth = Screen.width;

            EditorGUILayout.BeginHorizontal();
                
            var spaceAllocated = 0f;
            var selectedCount = 0;
            foreach (var transition in EnumExtension.GetValues<StateTransition>())
            {
                if (transition == StateTransition.None) continue;

                var transitionName = transition.ToString();

                var contains = (AllowedTransitions & transition) == transition;

                if (contains) selectedCount++;

                var buttonSize = GUI.skin.label.CalcSize(new GUIContent(transitionName)).x + 15;
                spaceAllocated += buttonSize;

                if (spaceAllocated > inspectorWidth)
                {
                    spaceAllocated = buttonSize;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                var oldColor = GUI.color;
                GUI.color = contains ? new Color(0, 153, 204) : GUI.color;
                    
                if (GUILayout.Button(transitionName, GUILayout.ExpandWidth(false)))
                {
                    if (contains)
                        AllowedTransitions &= ~transition;
                    else
                        AllowedTransitions |= transition;
                }

                GUI.color = oldColor;
            }
            EditorGUILayout.EndHorizontal();
            //}

            GUILayout.Label("Selected: " + selectedCount);

            GUILayout.Label("Custom Transition Queue", EditorStyles.boldLabel);
            if (CustomTransitionQueue == null)
                CustomTransitionQueue = new List<StateTransition>();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", GUILayout.MinWidth(30), GUILayout.ExpandWidth(false)))
                CustomTransitionQueue.Add(StateTransition.None);
            if (GUILayout.Button("-", GUILayout.MinWidth(30), GUILayout.ExpandWidth(false)) &&
                CustomTransitionQueue.Any())
                CustomTransitionQueue.RemoveAt(CustomTransitionQueue.Count - 1);
            EditorGUILayout.EndHorizontal();
            
            var transitionQueue = new List<StateTransition>();
            foreach (var transition in CustomTransitionQueue)
            {
                transitionQueue
                    .Add((StateTransition)EditorGUILayout.EnumPopup(transition));
            }
            CustomTransitionQueue = transitionQueue;
        }
#endif
    }
}
