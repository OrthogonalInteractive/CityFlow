#nullable enable

using CityFlow.Editor;
using CityFlow.Infrastructure.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityFlow.Tests.EditMode
{
    public sealed class NodePlacementInspectorTests
    {
        private sealed class InspectionWindow : EditorWindow { }
        [TestCase("Nodes.Array.data[0]")]
        [TestCase("Waves.Array.data[0].Additions.Array.data[0]")]
        public void RoleSelectorExposesOnlyApplicableFieldsAndSupportsUndo(string path)
        {
            var asset = ScriptableObject.CreateInstance<StageConfiguration>();
            var window = ScriptableObject.CreateInstance<InspectionWindow>();
            window.Show();
            try
            {
                asset.Nodes = new NodePlacement[] { new SourceNodePlacement { Id = "S", Position = Vector3.right * 7, MaxOutgoing = 2 } };
                asset.Waves = new[] { new StageConfiguration.WavePlacement { Additions = asset.Nodes } };
                using var serialized = new SerializedObject(asset);
                var property = serialized.FindProperty(path);
                Assert.That(property.FindPropertyRelative("MaxIncoming"), Is.Null);
                Assert.That(property.FindPropertyRelative("SinkColor"), Is.Null);
                var root = new NodePlacementDrawer().CreatePropertyGUI(property);
                window.rootVisualElement.Add(root);
                var selector = root.Q<DropdownField>("node-kind");
                Assert.That(selector.value, Is.EqualTo("Source"));
                Undo.IncrementCurrentGroup();
                selector.value = "Relay";
                property = serialized.FindProperty(path);
                Assert.That(property.managedReferenceValue, Is.TypeOf<RelayNodePlacement>());
                Assert.That(property.FindPropertyRelative("MaxOutgoing").intValue, Is.EqualTo(2));
                Assert.That(property.FindPropertyRelative("MaxIncoming").intValue, Is.EqualTo(3));
                Assert.That(property.FindPropertyRelative("GenerationInterval"), Is.Null);
                Undo.IncrementCurrentGroup();
                selector.value = "Sink";
                property = serialized.FindProperty(path);
                Assert.That(property.managedReferenceValue, Is.TypeOf<SinkNodePlacement>());
                Assert.That(property.FindPropertyRelative("MaxOutgoing"), Is.Null);
                Assert.That(property.FindPropertyRelative("SinkColor"), Is.Not.Null);
                Assert.That(property.FindPropertyRelative("Id").stringValue, Is.EqualTo("S"));
                Assert.That(property.FindPropertyRelative("Position").vector3Value, Is.EqualTo(Vector3.right * 7));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                serialized.Update();
                Assert.That(serialized.FindProperty(path).managedReferenceValue, Is.TypeOf<RelayNodePlacement>());
            }
            finally
            {
                window.Close();
                Undo.ClearUndo(asset);
                Object.DestroyImmediate(asset);
            }
        }

        [Test] public void EmptyArrayElementCanBeAssignedARole()
        {
            var asset = ScriptableObject.CreateInstance<StageConfiguration>();
            var window = ScriptableObject.CreateInstance<InspectionWindow>();
            window.Show();
            try
            {
                asset.Nodes = new NodePlacement[1];
                using var serialized = new SerializedObject(asset);
                var root = new NodePlacementDrawer().CreatePropertyGUI(serialized.FindProperty("Nodes.Array.data[0]"));
                window.rootVisualElement.Add(root);
                root.Q<DropdownField>("node-kind").value = "Source";
                Assert.That(asset.Nodes[0], Is.TypeOf<SourceNodePlacement>());
                Assert.That(((SourceNodePlacement)asset.Nodes[0]).GenerationInterval, Is.EqualTo(3));
            }
            finally
            {
                window.Close();
                Undo.ClearUndo(asset);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
