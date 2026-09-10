// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using KINEMATION.Shared.KAnimationCore.Runtime.Rig;

using System;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif
using UnityEngine;

namespace KINEMATION.RetargetPro.Runtime.Features
{
#if UNITY_EDITOR
    public enum RetargetFeatureInitializationMessageChannel
    {
        Notification,
        Help
    }

    public readonly struct RetargetFeatureInitializationMessage
    {
        public readonly RetargetFeatureInitializationMessageChannel channel;
        public readonly MessageType type;
        public readonly RetargetFeature feature;
        public readonly string featureDisplayName;
        public readonly string text;

        public RetargetFeatureInitializationMessage(RetargetFeatureInitializationMessageChannel channel,
            MessageType type, RetargetFeature feature, string featureDisplayName, string text)
        {
            this.channel = channel;
            this.type = type;
            this.feature = feature;
            this.featureDisplayName = string.IsNullOrWhiteSpace(featureDisplayName)
                ? string.Empty
                : featureDisplayName.Trim();
            this.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }
    }
#endif

    [Serializable]
    public abstract class RetargetFeature : ScriptableObject, IRigUser, IRigProvider
    {
        [HideInInspector] public KRig sourceRig;
        [HideInInspector] public KRig targetRig;
        
        [Header("Feature")]
        [Tooltip("Overall influence of this feature. Set to 0 to disable it without removing it from the profile.")]
        [Range(0f, 1f)] public float featureWeight = 1f;
#if UNITY_EDITOR
        [NonSerialized] public bool drawGizmos = false;
#endif
        
        public virtual RetargetFeatureState CreateFeatureState()
        {
            return null;
        }

        public KRig GetRigAsset()
        {
            return targetRig;
        }
        
#if UNITY_EDITOR
        public virtual bool GetStatus()
        {
            return true;
        }

        public virtual string GetErrorMessage()
        {
            return "";
        }

        public virtual void CollectInitializationMessages(RetargetFeatureState state,
            List<RetargetFeatureInitializationMessage> messages)
        {
            if (messages == null)
            {
                return;
            }

            if (!GetStatus())
            {
                AddInitializationHelpMessage(messages, GetErrorMessage(), MessageType.Warning);
                return;
            }

            if (state != null && !state.IsValid())
            {
                AddInitializationHelpMessage(messages,
                    "Configured bones could not be resolved on the preview rigs. Rebuild the profile rigs or remap the feature.",
                    MessageType.Warning);
            }
        }

        public virtual void OnRigUpdated()
        {
        }

        public virtual void OnFeatureAdded()
        {
        }

        public virtual void MapChains()
        {

        }

        public virtual string GetDisplayName()
        {
            return GetType().Name;
        }

        protected void AddInitializationNotificationMessage(List<RetargetFeatureInitializationMessage> messages,
            string message, MessageType type = MessageType.Info)
        {
            AddInitializationMessage(messages, message, type, RetargetFeatureInitializationMessageChannel.Notification);
        }

        protected void AddInitializationHelpMessage(List<RetargetFeatureInitializationMessage> messages, string message,
            MessageType type = MessageType.Info)
        {
            AddInitializationMessage(messages, message, type, RetargetFeatureInitializationMessageChannel.Help);
        }

        private void AddInitializationMessage(List<RetargetFeatureInitializationMessage> messages, string message,
            MessageType type, RetargetFeatureInitializationMessageChannel channel)
        {
            if (messages == null)
            {
                return;
            }

            string normalizedMessage = NormalizeInitializationMessage(message);
            if (string.IsNullOrEmpty(normalizedMessage))
            {
                return;
            }

            messages.Add(new RetargetFeatureInitializationMessage(channel, type,
                this, GetInitializationMessagePrefix(), normalizedMessage));
        }

        private string GetInitializationMessagePrefix()
        {
            string displayName = NormalizeInitializationMessage(GetDisplayName());
            return string.IsNullOrEmpty(displayName) ? GetType().Name : displayName;
        }

        private static string NormalizeInitializationMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }
#endif
        public KRigElement[] GetHierarchy()
        {
            return targetRig == null ? null : targetRig.GetHierarchy();
        }
    }
}