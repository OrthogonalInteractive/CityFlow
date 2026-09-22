#nullable enable

using System.Collections.Generic;
using CityFlow.Infrastructure.Configuration;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CityFlow.Editor
{
    [CustomPropertyDrawer(typeof(NodePlacement), true)]
    public sealed class NodePlacementDrawer : PropertyDrawer
    {
        private const string TemplatePath = "Assets/CityFlow/Editor/NodePlacement.uxml";
        private static readonly List<string> Kinds = new() { "Choose Node type", "Source", "Relay", "Sink" };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath).CloneTree();
            var foldout = root.Q<Foldout>("placement");
            foldout.text = property.displayName;
            var kind = root.Q<DropdownField>("node-kind");
            kind.choices = Kinds;
            var fields = root.Q<VisualElement>("placement-fields");
            var description = root.Q<Label>("direction-description");
            string path = property.propertyPath;
            var serialized = property.serializedObject;
            string? renderedType = null;

            void Refresh()
            {
                var current = serialized.FindProperty(path);
                string type = current.managedReferenceFullTypename;
                if (renderedType == type) return;
                renderedType = type;
                fields.Unbind();
                fields.Clear();
                var placement = current.managedReferenceValue as NodePlacement;
                kind.SetValueWithoutNotify(placement?.Kind.ToString() ?? Kinds[0]);
                description.text = placement switch
                {
                    SourceNodePlacement => "OUT only · generates FLOW into its own Buffer",
                    RelayNodePlacement => "IN / OUT · receives and forwards buffered FLOW",
                    SinkNodePlacement => "IN only · consumes matching FLOW on arrival",
                    _ => "Select the role to configure this Node."
                };
                if (placement == null) return;
                var child = current.Copy();
                var end = child.GetEndProperty();
                if (!child.NextVisible(true)) return;
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    fields.Add(new PropertyField(child.Copy()));
                } while (child.NextVisible(false));
                fields.Bind(serialized);
            }

            kind.RegisterValueChangedCallback(change =>
            {
                if (change.newValue == Kinds[0])
                {
                    renderedType = null;
                    Refresh();
                    return;
                }
                // SerializedProperty records Undo and keeps the shared identity when changing roles.
                foreach (var target in serialized.targetObjects)
                {
                    using var editing = new SerializedObject(target);
                    var current = editing.FindProperty(path);
                    var old = current.managedReferenceValue as NodePlacement;
                    NodePlacement replacement = change.newValue switch
                    {
                        "Source" => new SourceNodePlacement(),
                        "Relay" => new RelayNodePlacement(),
                        _ => new SinkNodePlacement()
                    };
                    if (old != null)
                    {
                        replacement.Id = old.Id;
                        replacement.Position = old.Position;
                        var definition = old.ToDefinition();
                        if (replacement is SourceNodePlacement source && old is not SinkNodePlacement)
                            source.MaxOutgoing = definition.MaxOutgoing;
                        if (replacement is SinkNodePlacement sink && old is not SourceNodePlacement)
                            sink.MaxIncoming = definition.MaxIncoming;
                        if (replacement is RelayNodePlacement relay)
                        {
                            if (old is not SourceNodePlacement) relay.MaxIncoming = definition.MaxIncoming;
                            if (old is not SinkNodePlacement) relay.MaxOutgoing = definition.MaxOutgoing;
                        }
                    }
                    current.managedReferenceValue = replacement;
                    editing.ApplyModifiedProperties();
                }
                serialized.Update();
                Refresh();
            });
            root.TrackPropertyValue(property, _ => Refresh());
            Refresh();
            return root;
        }
    }
}
