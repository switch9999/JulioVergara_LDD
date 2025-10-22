namespace DevelopersHub.ProceduralTilemapGenerator2D.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using System.Collections.Generic;

    [CustomEditor(typeof(TilemapGenerator2D))]
    public class EditorTilemapGenerator2D : Editor
    {
        private TilemapGenerator2D _generator2D = null;
        private Texture2D baseHeightsNoiseTextures = null;
        private Texture2D[] baseObjectsNoiseTextures = null;
        private Texture2D[] noiseTextures = null;
        private Texture2D[] heightNoiseTextures = null;
        private List<Texture2D[]> objectNoiseTextures = null;
        private SerializedProperty _generateOnAwake = null;
        private SerializedProperty type = null;
        private SerializedProperty baseCollider = null;
        private SerializedProperty tilemapSize = null;
        private SerializedProperty staticPosition = null;
        // private SerializedProperty multiFrame = null;
        private SerializedProperty placeTilesPerFrame = null;
        private SerializedProperty clearTilesPerFrame = null;
        private SerializedProperty regenerateDistance = null;
        //private SerializedProperty colliderThickness = null;
        private SerializedProperty infiniteTarget = null;
        private SerializedProperty baseTile = null;
        private SerializedProperty baseHeights = null;
        private SerializedProperty baseObjects = null;
        private SerializedProperty tilesets = null;

        private readonly string[] toolbarStrings = { "General", "Tilesets", "Events" };
        private GUIContent trashcanIcon = null;

        private void OnEnable()
        {
            _generator2D = (TilemapGenerator2D)target;
            trashcanIcon = EditorGUIUtility.IconContent("d_winbtn_mac_close");
            _generateOnAwake = serializedObject.FindProperty("_generateOnAwake");
            type = serializedObject.FindProperty("type");
            tilemapSize = serializedObject.FindProperty("tilemapSize");
            staticPosition = serializedObject.FindProperty("staticPosition");
            infiniteTarget = serializedObject.FindProperty("infiniteTarget");
            baseTile = serializedObject.FindProperty("baseTile");
            baseHeights = serializedObject.FindProperty("baseHeights");
            baseObjects = serializedObject.FindProperty("baseObjects");
            tilesets = serializedObject.FindProperty("tilesets");
            baseCollider = serializedObject.FindProperty("baseCollider");
            //multiFrame = serializedObject.FindProperty("multiFrame");
            placeTilesPerFrame = serializedObject.FindProperty("placeTilesPerFrame");
            clearTilesPerFrame = serializedObject.FindProperty("clearTilesPerFrame");
            regenerateDistance = serializedObject.FindProperty("regenerateDistance");
            //colliderThickness = serializedObject.FindProperty("colliderThickness");
            _generator2D.EditorNoiseDataUpdated();
            _generator2D.EditorInstanceSelected();
            UpdateNoiseTextures();
            UpdateBaseHeightNoiseTexture();
            UpdateBaseObjectsNoiseTexture();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.Space();
            _generator2D.editorTabIndex = GUILayout.Toolbar(_generator2D.editorTabIndex, toolbarStrings);
            EditorGUILayout.Space();
            if (Application.isPlaying) { EditorGUI.BeginDisabledGroup(true); }
            switch (_generator2D.editorTabIndex)
            {
                case 0:
                    DrawGeneralTab();
                    break;
                case 1:
                    DrawTilesetsTab();
                    break;
                case 2:
                    DrawEventsTab();
                    break;
            }
            if (Application.isPlaying) { EditorGUI.EndDisabledGroup(); }
            if (GUILayout.Button("Generate"))
            {
                _generator2D.Generate(TilemapGenerator2D.GenerateType.Immediate, TilemapGenerator2D.ClearType.ClearPreviousImmediate);
                UpdateNoiseTextures();
            }
            if (GUILayout.Button("Clear"))
            {
                _generator2D.EditorClear();
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneralTab()
        {
            type.enumValueIndex = (int)(TilemapGenerator2D.Type)EditorGUILayout.EnumPopup("Type", (TilemapGenerator2D.Type)Enum.GetValues(typeof(TilemapGenerator2D.Type)).GetValue(type.enumValueIndex));
            tilemapSize.intValue = EditorGUILayout.IntField("Size", tilemapSize.intValue);
            if (tilemapSize.intValue < 1) { tilemapSize.intValue = 1; }
            if (type.enumValueIndex == 0)
            {
                _generateOnAwake.boolValue = EditorGUILayout.Toggle("Generate On Awake", _generateOnAwake.boolValue);
                staticPosition.vector3Value = EditorGUILayout.Vector3Field("Target", staticPosition.vector3Value);
            }
            else
            {
                infiniteTarget.objectReferenceValue = EditorGUILayout.ObjectField("Target", infiniteTarget.objectReferenceValue, typeof(Transform), true);
                regenerateDistance.intValue = EditorGUILayout.IntSlider("Regenerate Distance", regenerateDistance.intValue, 1, 10);
                placeTilesPerFrame.intValue = EditorGUILayout.IntSlider("Place Per Frame", placeTilesPerFrame.intValue, 1, tilemapSize.intValue);
                clearTilesPerFrame.intValue = EditorGUILayout.IntSlider("Clear Per Frame", clearTilesPerFrame.intValue, 1, tilemapSize.intValue);
            }
            EditorGUILayout.Space();
        }

        private void DrawTilesetsTab()
        {
            // Base Tile, Base Heights, and Base Objects Container
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            baseTile.objectReferenceValue = EditorGUILayout.ObjectField("Base Tile", baseTile.objectReferenceValue, typeof(Sprite), false);
            baseCollider.enumValueIndex = (int)(TilemapGenerator2D.ColliderType)EditorGUILayout.EnumPopup("Base Collider", (TilemapGenerator2D.ColliderType)Enum.GetValues(typeof(TilemapGenerator2D.ColliderType)).GetValue(baseCollider.enumValueIndex));
            /*
            colliderThickness.floatValue = EditorGUILayout.FloatField("Collider Thickness", colliderThickness.floatValue);
            if (colliderThickness.floatValue < 0.01f || colliderThickness.floatValue > 1f)
            {
                colliderThickness.floatValue = Mathf.Clamp(colliderThickness.floatValue, 0.01f, 1f);
            }
            */
            if (baseHeights != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool heightExpanded = EditorGUILayout.Foldout(baseHeights.isExpanded, "Base Cliff Data", true);
                if (heightExpanded != baseHeights.isExpanded)
                {
                    baseHeights.isExpanded = heightExpanded;
                }
                if (baseHeights.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("ruleTile"));
                    bool assigned = baseHeights.FindPropertyRelative("ruleTile").objectReferenceValue != null;
                    if (assigned)
                    {
                        SerializedProperty colliderThickness = baseHeights.FindPropertyRelative("colliderThickness");
                        EditorGUILayout.PropertyField(colliderThickness);
                        if (colliderThickness.floatValue < 0.01f || colliderThickness.floatValue > 1f)
                        {
                            colliderThickness.floatValue = Mathf.Clamp(colliderThickness.floatValue, 0.01f, 1f);
                        }
                        SerializedProperty colliderHorizontalPadding = baseHeights.FindPropertyRelative("colliderHorizontalPadding");
                        EditorGUILayout.PropertyField(colliderHorizontalPadding);
                        if (colliderHorizontalPadding.floatValue < 0 || colliderHorizontalPadding.floatValue > 0.25f)
                        {
                            colliderHorizontalPadding.floatValue = Mathf.Clamp(colliderHorizontalPadding.floatValue, 0, 0.25f);
                        }
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("topSlope"));
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("rightSlope"));
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("leftSlope"));
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("bottomSlope"));
                        SerializedProperty frequency = baseHeights.FindPropertyRelative("slopeFrequency");
                        EditorGUILayout.PropertyField(frequency);
                        if (frequency.intValue < 0 || frequency.intValue > 10)
                        {
                            frequency.intValue = Mathf.Clamp(frequency.intValue, 0, 10);
                        }
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("noiseType"));
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("noiseSeed"));
                        EditorGUILayout.PropertyField(baseHeights.FindPropertyRelative("noiseScale"));
                        SerializedProperty threshold = baseHeights.FindPropertyRelative("threshold");
                        EditorGUILayout.PropertyField(threshold);
                        if (threshold.floatValue < 0 || threshold.floatValue > 1)
                        {
                            threshold.floatValue = Mathf.Clamp(threshold.floatValue, 0, 1);
                        }
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        if (assigned)
                        {
                            _generator2D.EditorNoiseDataUpdated();
                            UpdateBaseHeightNoiseTexture();
                        }
                    }

                    if (assigned)
                    {
                        // Display Height Noise Texture
                        if (assigned && baseHeightsNoiseTextures != null)
                        {
                            Rect heightRect = GUILayoutUtility.GetRect(100, 100);
                            EditorGUI.DrawPreviewTexture(heightRect, baseHeightsNoiseTextures);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            // Base Objects
            if (baseObjects != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool objectsExpanded = EditorGUILayout.Foldout(baseObjects.isExpanded, "Base Objects", true);
                if (objectsExpanded != baseObjects.isExpanded)
                {
                    baseObjects.isExpanded = objectsExpanded;
                }
                if (baseObjects.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < baseObjects.arraySize; i++)
                    {
                        DrawTilemapObjectData(baseObjects, i, -1); // -1 indicates base objects
                    }
                    if (GUILayout.Button("Add Base Object"))
                    {
                        baseObjects.arraySize++;
                        serializedObject.ApplyModifiedProperties();
                        UpdateBaseObjectsNoiseTexture(); // Update noise textures after adding
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical(); // End Base Data container

            EditorGUILayout.Space();

            // Tilesets
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < tilesets.arraySize; i++)
            {
                SerializedProperty tilesetData = tilesets.GetArrayElementAtIndex(i);
                DrawTilesetData(tilesetData, i);
            }
            if (GUILayout.Button("Add Tileset"))
            {
                tilesets.arraySize++;
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _generator2D.EditorNoiseDataUpdated();
                UpdateNoiseTextures();
            }
        }

        private void DrawTilesetData(SerializedProperty tilesetData, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            bool isExpanded = EditorGUILayout.Foldout(tilesetData.isExpanded, $"Tileset {index + 1}", true);
            if (isExpanded != tilesetData.isExpanded)
            {
                tilesetData.isExpanded = isExpanded;
            }
            if (GUILayout.Button(trashcanIcon, GUILayout.Width(20), GUILayout.Height(20)))
            {
                tilesets.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                _generator2D.EditorNoiseDataUpdated();
                UpdateNoiseTextures();
                return; // Exit early to avoid index issues
            }
            EditorGUILayout.EndHorizontal();
            if (tilesetData.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(tilesetData.FindPropertyRelative("ruleTile"));
                if (tilesetData.FindPropertyRelative("ruleTile").objectReferenceValue != null)
                {
                    var col = tilesetData.FindPropertyRelative("collider");
                    EditorGUILayout.PropertyField(col);
                    if (col.enumValueIndex != (int)TilemapGenerator2D.ColliderType.Walkable)
                    {
                        var th = tilesetData.FindPropertyRelative("colliderThickness");
                        EditorGUILayout.PropertyField(th);
                        if (th.floatValue < 0.01f || th.floatValue > 1f)
                        {
                            th.floatValue = Mathf.Clamp(th.floatValue, 0.01f, 1f);
                        }
                    }
                    EditorGUILayout.PropertyField(tilesetData.FindPropertyRelative("priority"));
                    SerializedProperty trhd = tilesetData.FindPropertyRelative("threshold");
                    EditorGUILayout.PropertyField(trhd);
                    if (trhd.floatValue < 0 || trhd.floatValue > 1)
                    {
                        trhd.floatValue = Mathf.Clamp(trhd.floatValue, 0, 1);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                    }
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(tilesetData.FindPropertyRelative("noiseType"));
                    EditorGUILayout.PropertyField(tilesetData.FindPropertyRelative("noiseSeed"));
                    EditorGUILayout.PropertyField(tilesetData.FindPropertyRelative("noiseScale"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        UpdateNoiseTexture(index);
                    }

                    // Display Noise Texture
                    if (noiseTextures != null && index < noiseTextures.Length && noiseTextures[index] != null)
                    {
                        Rect rect = GUILayoutUtility.GetRect(100, 100);
                        EditorGUI.DrawPreviewTexture(rect, noiseTextures[index]);
                    }

                    // Tileset Height Data
                    SerializedProperty heightData = tilesetData.FindPropertyRelative("heights");
                    if (heightData != null)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        bool heightExpanded = EditorGUILayout.Foldout(heightData.isExpanded, "Cliff Data", true);
                        if (heightExpanded != heightData.isExpanded)
                        {
                            heightData.isExpanded = heightExpanded;
                        }
                        if (heightData.isExpanded)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(heightData.FindPropertyRelative("ruleTile"));
                            bool assigned = heightData.FindPropertyRelative("ruleTile").objectReferenceValue != null;
                            if (assigned)
                            {
                                SerializedProperty colliderThickness = heightData.FindPropertyRelative("colliderThickness");
                                EditorGUILayout.PropertyField(colliderThickness);
                                if (colliderThickness.floatValue < 0.01f || colliderThickness.floatValue > 1f)
                                {
                                    colliderThickness.floatValue = Mathf.Clamp(colliderThickness.floatValue, 0.01f, 1f);
                                }
                                SerializedProperty colliderHorizontalPadding = heightData.FindPropertyRelative("colliderHorizontalPadding");
                                EditorGUILayout.PropertyField(colliderHorizontalPadding);
                                if (colliderHorizontalPadding.floatValue < 0 || colliderHorizontalPadding.floatValue > 0.25f)
                                {
                                    colliderHorizontalPadding.floatValue = Mathf.Clamp(colliderHorizontalPadding.floatValue, 0, 0.25f);
                                }
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("topSlope"));
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("rightSlope"));
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("leftSlope"));
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("bottomSlope"));
                                SerializedProperty frequency = heightData.FindPropertyRelative("slopeFrequency");
                                EditorGUILayout.PropertyField(frequency);
                                if (frequency.intValue < 0 || frequency.intValue > 10)
                                {
                                    frequency.intValue = Mathf.Clamp(frequency.intValue, 0, 10);
                                }
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("noiseType"));
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("noiseSeed"));
                                EditorGUILayout.PropertyField(heightData.FindPropertyRelative("noiseScale"));
                                SerializedProperty threshold = heightData.FindPropertyRelative("threshold");
                                EditorGUILayout.PropertyField(threshold);
                                if (threshold.floatValue < 0 || threshold.floatValue > 1)
                                {
                                    threshold.floatValue = Mathf.Clamp(threshold.floatValue, 0, 1);
                                }
                            }
                            if (EditorGUI.EndChangeCheck())
                            {
                                serializedObject.ApplyModifiedProperties();
                                UpdateNoiseTextures();
                            }

                            if (assigned)
                            {
                                // Display Height Noise Texture
                                if (heightNoiseTextures != null && index < heightNoiseTextures.Length && heightNoiseTextures[index] != null)
                                {
                                    Rect heightRect = GUILayoutUtility.GetRect(100, 100);
                                    EditorGUI.DrawPreviewTexture(heightRect, heightNoiseTextures[index]);
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }

                    // Tilemap Object Data
                    SerializedProperty objects = tilesetData.FindPropertyRelative("objects");
                    if (objects != null && objects.isArray)
                    {
                        for (int i = 0; i < objects.arraySize; i++)
                        {
                            DrawTilemapObjectData(objects, i, index);
                        }
                        if (GUILayout.Button("Add Object"))
                        {
                            objects.arraySize++;
                            serializedObject.ApplyModifiedProperties();
                            UpdateNoiseTextures();
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private bool DrawTilemapObjectData(SerializedProperty objects, int objectIndex, int tilesetIndex)
        {
            bool changed = false;
            SerializedProperty objectData = objects.GetArrayElementAtIndex(objectIndex);
            if (objectData == null)
            {
                return false;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            bool isExpanded = EditorGUILayout.Foldout(objectData.isExpanded, $"Object {objectIndex + 1}", true);
            if (isExpanded != objectData.isExpanded)
            {
                objectData.isExpanded = isExpanded;
            }
            if (GUILayout.Button(trashcanIcon, GUILayout.Width(20), GUILayout.Height(20)))
            {
                objects.DeleteArrayElementAtIndex(objectIndex);
                serializedObject.ApplyModifiedProperties();
                if (tilesetIndex == -1)
                {
                    UpdateBaseObjectsNoiseTexture();
                }
                else
                {
                    UpdateNoiseTextures();
                }
                return true; // Exit early to avoid index issues
            }
            EditorGUILayout.EndHorizontal();

            if (objectData.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                
                // Draw the "type" field and get its value
                SerializedProperty typeProperty = objectData.FindPropertyRelative("type");
                EditorGUILayout.PropertyField(typeProperty);

                // Check the value of the "type" field
                TilemapGenerator2D.ObjectType objectType = (TilemapGenerator2D.ObjectType)typeProperty.enumValueIndex;
                
                // Conditionally show/hide fields based on the "type" value
                if (objectType == TilemapGenerator2D.ObjectType.Prefab)
                {
                    EditorGUILayout.PropertyField(objectData.FindPropertyRelative("prefabs")); // Show prefabs array
                }
                else if (objectType == TilemapGenerator2D.ObjectType.Sprite)
                {
                    SerializedProperty col = objectData.FindPropertyRelative("colliderType");
                    EditorGUILayout.PropertyField(col);
                    if (col .enumValueIndex != (int)TilemapGenerator2D.Collider2DType.None)
                    {
                        EditorGUILayout.PropertyField(objectData.FindPropertyRelative("colliderSize"));
                    }
                    EditorGUILayout.PropertyField(objectData.FindPropertyRelative("sprites")); // Show sprites array
                }

                EditorGUILayout.PropertyField(objectData.FindPropertyRelative("priority"));
                EditorGUILayout.PropertyField(objectData.FindPropertyRelative("scale"));
                EditorGUILayout.PropertyField(objectData.FindPropertyRelative("noiseType"));
                EditorGUILayout.PropertyField(objectData.FindPropertyRelative("noiseSeed"));
                EditorGUILayout.PropertyField(objectData.FindPropertyRelative("noiseScale"));
                SerializedProperty threshold = objectData.FindPropertyRelative("threshold");
                EditorGUILayout.PropertyField(threshold);
                if (threshold.floatValue < 0 || threshold.floatValue > 1)
                {
                    threshold.floatValue = Mathf.Clamp(threshold.floatValue, 0, 1);
                }
                EditorGUILayout.PropertyField(objectData.FindPropertyRelative("coverHeights"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    _generator2D.EditorNoiseDataUpdated();
                    if (tilesetIndex == -1)
                    {
                        UpdateBaseObjectsNoiseTexture(); // Update noise textures after modification
                    }
                    else
                    {
                        UpdateNoiseTextures();
                    }
                    changed = true;
                }

                // Display Object Noise Texture
                if (tilesetIndex >= 0 && tilesetIndex < objectNoiseTextures.Count && objectNoiseTextures[tilesetIndex] != null && objectIndex >= 0 && objectIndex < objectNoiseTextures[tilesetIndex].Length)
                {
                    Rect objectRect = GUILayoutUtility.GetRect(100, 100);
                    EditorGUI.DrawPreviewTexture(objectRect, objectNoiseTextures[tilesetIndex][objectIndex]);
                }
                else if (tilesetIndex == -1 && baseObjectsNoiseTextures != null && objectIndex >= 0 && objectIndex < baseObjectsNoiseTextures.Length)
                {
                    Rect objectRect = GUILayoutUtility.GetRect(100, 100);
                    EditorGUI.DrawPreviewTexture(objectRect, baseObjectsNoiseTextures[objectIndex]);
                }
                else
                {
                    Debug.LogWarning("Noise texture not available for object at index " + objectIndex);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            return changed;
        }

        private void DrawEventsTab()
        {
            EditorGUILayout.Space();
        }

        private void UpdateNoiseTextures()
        {
            noiseTextures = new Texture2D[tilesets.arraySize];
            heightNoiseTextures = new Texture2D[tilesets.arraySize];
            objectNoiseTextures = new List<Texture2D[]>();
            for (int i = 0; i < tilesets.arraySize; i++)
            {
                var tileset = tilesets.GetArrayElementAtIndex(i).GetValue<TilemapGenerator2D.TilesetData>();
                noiseTextures[i] = tileset.noise.GenerateTexture(128, 128);

                if (tileset.heights != null)
                {
                    heightNoiseTextures[i] = tileset.heights.noise.GenerateTexture(128, 128);
                }

                if (tileset.objects != null)
                {
                    Texture2D[] objectTextures = new Texture2D[tileset.objects.Count];
                    for (int j = 0; j < tileset.objects.Count; j++)
                    {
                        objectTextures[j] = tileset.objects[j].noise.GenerateTexture(128, 128);
                    }
                    objectNoiseTextures.Add(objectTextures);
                }
                else
                {
                    objectNoiseTextures.Add(new Texture2D[0]); // Add an empty array if no objects exist
                }
            }
        }

        private void UpdateBaseHeightNoiseTexture()
        {
            if (baseHeights != null)
            {
                baseHeightsNoiseTextures = baseHeights.GetValue<TilemapGenerator2D.TilesetHeightData>().noise.GenerateTexture(128, 128);
            }
        }

        private void UpdateBaseObjectsNoiseTexture()
        {
            if (baseObjects != null)
            {
                baseObjectsNoiseTextures = new Texture2D[baseObjects.arraySize];
                for (int i = 0; i < baseObjects.arraySize; i++)
                {
                    var objectData = baseObjects.GetArrayElementAtIndex(i).GetValue<TilemapGenerator2D.TilemapObjectData>();
                    if (objectData != null && objectData.noise != null)
                    {
                        baseObjectsNoiseTextures[i] = objectData.noise.GenerateTexture(128, 128);
                    }
                    else
                    {
                        baseObjectsNoiseTextures[i] = Texture2D.blackTexture; // Fallback texture
                    }
                }
            }
            else
            {
                baseObjectsNoiseTextures = new Texture2D[0]; // Empty array if baseObjects is null
            }
        }

        private void UpdateNoiseTexture(int index)
        {
            var tileset = tilesets.GetArrayElementAtIndex(index).GetValue<TilemapGenerator2D.TilesetData>();
            noiseTextures[index] = tileset.noise.GenerateTexture(128, 128);
            if (tileset.heights != null)
            {
                heightNoiseTextures[index] = tileset.heights.noise.GenerateTexture(128, 128);
            }
            if (tileset.objects != null)
            {
                for (int i = 0; i < tileset.objects.Count; i++)
                {
                    objectNoiseTextures[index][i] = tileset.objects[i].noise.GenerateTexture(128, 128);
                }
            }
        }
        
    }
}