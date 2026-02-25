using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GPUInfiniteGrass {

    public partial class ParticleSurfaceManagerInspector {

        // Draws Surfaces + Surface Meshes table
        private void DrawSurfaceList(SerializedProperty surfaces, SerializedProperty surfaceMeshes) {
            if (surfaces == null || surfaceMeshes == null) return;
            EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
            ClearSurfaceMeshesForMissingSurfaces(surfaces, surfaceMeshes);
            EnsureSurfaceList(surfaces, surfaceMeshes);
            Rect titleRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            HandleSurfaceDropArea(titleRect, surfaces, surfaceMeshes);
            EditorGUI.LabelField(titleRect, _surfacesTitleContent, EditorStyles.boldLabel);
            _surfaceList.DoLayoutList();
        }

        // Builds/rebuilds reorderable list
        private void EnsureSurfaceList(SerializedProperty surfaces, SerializedProperty surfaceMeshes) {
            if (_surfaceList != null
                && _surfaceList.serializedProperty != null
                && _surfaceList.serializedProperty.serializedObject.targetObject == serializedObject.targetObject
                && _surfaceList.serializedProperty.propertyPath == surfaces.propertyPath) {
                return;
            }
            _surfaceList = new ReorderableList(serializedObject, surfaces, true, true, true, true);
            _surfaceList.drawHeaderCallback = rect => {
                HandleSurfaceDropArea(rect, surfaces, surfaceMeshes);
                Rect leftRect;
                Rect rightRect;
                Rect buttonRect;
                GetSurfaceListColumnRects(rect, out leftRect, out rightRect, out buttonRect);
                leftRect.xMin += _surfaceListHeaderLeftInset;
                rightRect.xMin += _surfaceListHeaderRightInset;
                EditorGUI.LabelField(leftRect, _surfaceColumnContent);
                EditorGUI.LabelField(rightRect, _surfaceMeshColumnContent);
            };
            _surfaceList.elementHeightCallback = index => EditorGUIUtility.singleLineHeight + 6f;
            _surfaceList.drawElementCallback = (rect, index, isActive, isFocused) => {
                EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
                if (index < 0 || index >= surfaces.arraySize || index >= surfaceMeshes.arraySize) return;
                SerializedProperty surface = surfaces.GetArrayElementAtIndex(index);
                SerializedProperty surfaceMesh = surfaceMeshes.GetArrayElementAtIndex(index);
                float lineHeight = EditorGUIUtility.singleLineHeight;
                Rect rowRect = new Rect(rect.x, rect.y + 2f, rect.width, lineHeight);
                int previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                try {
                    Rect leftRect;
                    Rect rightRect;
                    Rect buttonRect;
                    GetSurfaceListColumnRects(rowRect, out leftRect, out rightRect, out buttonRect);
                    Object previousSurface = surface.objectReferenceValue;
                    surface.objectReferenceValue = DrawInlineObjectField(leftRect, surface.objectReferenceValue, typeof(Transform), true, _surfaceColumnContent.tooltip);
                    bool surfaceChanged = surface.objectReferenceValue != previousSurface;
                    if (surfaceChanged) {
                        surfaceMesh.objectReferenceValue = null;
                        Transform assignedSurface = surface.objectReferenceValue as Transform;
                        if (assignedSurface != null && HasSupportedSurfaceSource(assignedSurface))
                            surfaceMesh.objectReferenceValue = CreateAndTrackSurfaceMeshCopy(assignedSurface);
                    }
                    if (surface.objectReferenceValue == null) {
                        using (new EditorGUI.DisabledScope(true)) {
                            DrawInlineObjectField(rightRect, surfaceMesh.objectReferenceValue, typeof(Mesh), false, _surfaceMeshColumnContent.tooltip);
                            GUIContent regenerateButtonContent = GetRegenerateMeshButtonContent();
                            GUI.Button(buttonRect, regenerateButtonContent, EditorStyles.iconButton);
                        }
                        return;
                    }
                    surfaceMesh.objectReferenceValue = DrawInlineObjectField(rightRect, surfaceMesh.objectReferenceValue, typeof(Mesh), false, _surfaceMeshColumnContent.tooltip);
                    Transform assignedSurfaceForButton = surface.objectReferenceValue as Transform;
                    bool canRegenerate = assignedSurfaceForButton != null && HasSupportedSurfaceSource(assignedSurfaceForButton);
                    using (new EditorGUI.DisabledScope(!canRegenerate)) {
                        GUIContent regenerateButtonContent = GetRegenerateMeshButtonContent();
                        if (GUI.Button(buttonRect, regenerateButtonContent, EditorStyles.iconButton)) {
                            ParticleSurfaceManager manager = target as ParticleSurfaceManager;
                            if (manager != null) PopupWindow.Show(buttonRect, new RegenerateSurfaceMeshPopup(manager, index, assignedSurfaceForButton));
                        }
                    }
                } finally {
                    EditorGUI.indentLevel = previousIndent;
                }
            };
            _surfaceList.onAddCallback = list => {
                int newIndex = surfaces.arraySize;
                surfaces.arraySize++;
                EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
                surfaces.GetArrayElementAtIndex(newIndex).objectReferenceValue = null;
                surfaceMeshes.GetArrayElementAtIndex(newIndex).objectReferenceValue = null;
                list.index = newIndex;
            };
            _surfaceList.onRemoveCallback = list => {
                int index = list.index;
                if (index < 0 || index >= surfaces.arraySize) return;
                DeleteObjectArrayElement(surfaces, index);
                DeleteObjectArrayElement(surfaceMeshes, index);
                EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
                list.index = surfaces.arraySize > 0 ? Mathf.Clamp(index, 0, surfaces.arraySize - 1) : -1;
            };
            _surfaceList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) => {
                EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
                if (oldIndex < 0 || newIndex < 0 || oldIndex >= surfaceMeshes.arraySize || newIndex >= surfaceMeshes.arraySize) return;
                surfaceMeshes.MoveArrayElement(oldIndex, newIndex);
            };
            _surfaceList.onChangedCallback = list => {
                EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
                ClearSurfaceMeshesForMissingSurfaces(surfaces, surfaceMeshes);
            };
        }

        // Keeps SurfaceMeshes array in sync with Surfaces size
        private static void EnsureSurfaceMeshArraySize(SerializedProperty surfaces, SerializedProperty surfaceMeshes) {
            if (surfaces == null || surfaceMeshes == null || !surfaceMeshes.isArray || !surfaces.isArray) return;
            int targetSize = surfaces.arraySize;
            int previousSize = surfaceMeshes.arraySize;
            if (previousSize == targetSize) return;
            surfaceMeshes.arraySize = targetSize;
            if (targetSize <= previousSize) return;
            for (int i = previousSize; i < targetSize; i++) {
                surfaceMeshes.GetArrayElementAtIndex(i).objectReferenceValue = null;
            }
        }

        // Clears mesh slot when surface slot is empty
        private static void ClearSurfaceMeshesForMissingSurfaces(SerializedProperty surfaces, SerializedProperty surfaceMeshes) {
            if (surfaces == null || surfaceMeshes == null || !surfaceMeshes.isArray || !surfaces.isArray) return;
            int count = Mathf.Min(surfaces.arraySize, surfaceMeshes.arraySize);
            for (int i = 0; i < count; i++) {
                SerializedProperty surface = surfaces.GetArrayElementAtIndex(i);
                if (surface.objectReferenceValue != null) continue;
                SerializedProperty surfaceMesh = surfaceMeshes.GetArrayElementAtIndex(i);
                if (surfaceMesh.objectReferenceValue == null) continue;
                surfaceMesh.objectReferenceValue = null;
            }
        }

        private static void DeleteObjectArrayElement(SerializedProperty array, int index) {
            if (array == null || !array.isArray || index < 0 || index >= array.arraySize) return;
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            if (element.objectReferenceValue != null) {
                element.objectReferenceValue = null;
            }
            array.DeleteArrayElementAtIndex(index);
        }

        private static Object DrawInlineObjectField(Rect rect, Object current, System.Type objectType, bool allowSceneObjects, string tooltip) {
            Object result = EditorGUI.ObjectField(rect, GUIContent.none, current, objectType, allowSceneObjects);
            if (!string.IsNullOrEmpty(tooltip)) GUI.Label(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            return result;
        }

        // Accepts drag&drop on header/title area
        private static void HandleSurfaceDropArea(Rect rect, SerializedProperty surfaces, SerializedProperty surfaceMeshes) {
            Event e = Event.current;
            if (e == null) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!rect.Contains(e.mousePosition)) return;
            List<Transform> droppedSurfaces = ExtractDroppedSurfaces(DragAndDrop.objectReferences);
            if (droppedSurfaces.Count == 0) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform) {
                DragAndDrop.AcceptDrag();
                AddDroppedSurfaces(surfaces, surfaceMeshes, droppedSurfaces);
            }
            e.Use();
        }

        private static List<Transform> ExtractDroppedSurfaces(Object[] draggedObjects) {
            List<Transform> surfaces = new List<Transform>();
            if (draggedObjects == null || draggedObjects.Length == 0) return surfaces;

            HashSet<Transform> unique = new HashSet<Transform>();
            for (int i = 0; i < draggedObjects.Length; i++) {
                Object dragged = draggedObjects[i];
                if (dragged == null) continue;

                Transform surface = null;
                Transform transform = dragged as Transform;
                if (transform != null) surface = transform;
                if (surface == null) {
                    GameObject go = dragged as GameObject;
                    if (go != null) surface = go.transform;
                }
                if (surface == null) {
                    Component component = dragged as Component;
                    if (component != null) surface = component.transform;
                }
                if (surface == null || !HasSupportedSurfaceSource(surface) || unique.Contains(surface)) continue;

                unique.Add(surface);
                surfaces.Add(surface);
            }
            return surfaces;
        }

        private static void AddDroppedSurfaces(SerializedProperty surfaces, SerializedProperty surfaceMeshes, List<Transform> droppedSurfaces) {
            if (surfaces == null || surfaceMeshes == null || droppedSurfaces == null || droppedSurfaces.Count == 0) return;
            int startIndex = surfaces.arraySize;
            surfaces.arraySize = startIndex + droppedSurfaces.Count;
            EnsureSurfaceMeshArraySize(surfaces, surfaceMeshes);
            for (int i = 0; i < droppedSurfaces.Count; i++) {
                int targetIndex = startIndex + i;
                Transform droppedSurface = droppedSurfaces[i];
                surfaces.GetArrayElementAtIndex(targetIndex).objectReferenceValue = droppedSurface;
                Mesh generatedMesh = droppedSurface != null && HasSupportedSurfaceSource(droppedSurface)
                    ? CreateAndTrackSurfaceMeshCopy(droppedSurface)
                    : null;
                surfaceMeshes.GetArrayElementAtIndex(targetIndex).objectReferenceValue = generatedMesh;
            }
            GUI.changed = true;
        }

        private static bool HasSupportedSurfaceSource(Transform surface) {
            if (surface == null) return false;
            MeshFilter meshFilter = surface.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null) return true;
            Terrain terrain = surface.GetComponent<Terrain>();
            return terrain != null && terrain.terrainData != null;
        }

        private static void GetSurfaceListColumnRects(Rect rect, out Rect leftRect, out Rect rightRect, out Rect buttonRect) {
            float contentWidth = Mathf.Max(1f, rect.width - _surfaceListActionButtonWidth - _surfaceListColumnSpacing * 2f);
            float leftWidth = contentWidth * 0.5f;
            leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            rightRect = new Rect(leftRect.xMax + _surfaceListColumnSpacing, rect.y, contentWidth - leftWidth, rect.height);
            buttonRect = new Rect(rightRect.xMax + _surfaceListColumnSpacing, rect.y, _surfaceListActionButtonWidth, rect.height);
        }

        private static GUIContent GetRegenerateMeshButtonContent() {
            GUIContent icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Refresh" : "Refresh");
            if (icon != null && icon.image != null) {
                return new GUIContent(icon.image, _regenerateMeshTooltip);
            }
            return new GUIContent("R", _regenerateMeshTooltip);
        }

        private static GUIContent GetWarningIconContent() {
            GUIContent icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_console.warnicon.sml" : "console.warnicon.sml");
            if (icon != null && icon.image != null) return new GUIContent(icon.image);
            icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_console.warnicon" : "console.warnicon");
            if (icon != null && icon.image != null) return new GUIContent(icon.image);
            return new GUIContent("!");
        }

        private static GUIContent GetInfoIconContent() {
            GUIContent icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_console.infoicon.sml" : "console.infoicon.sml");
            if (icon != null && icon.image != null) return new GUIContent(icon.image);
            icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_console.infoicon" : "console.infoicon");
            if (icon != null && icon.image != null) return new GUIContent(icon.image);
            return new GUIContent("i");
        }

        // Popup for safe regenerate actions
        private sealed class RegenerateSurfaceMeshPopup : PopupWindowContent {
            private const float _popupWidth = 320f;
            private const float _popupPadding = 8f;
            private const float _rowSpacing = 4f;
            private const float _fieldSpacing = 2f;
            private const float _buttonSpacing = 6f;
            private const float _iconSize = 16f;
            private const float _iconGap = 4f;
            private readonly ParticleSurfaceManager _manager;
            private readonly int _surfaceIndex;
            private readonly Transform _surface;
            private readonly Terrain _terrain;
            private readonly bool _showTerrainResolution;
            private int _ratioIndex;

            private struct RegenerateMessageInfo {
                public string Text;
                public bool IsWarning;
            }

            public RegenerateSurfaceMeshPopup(ParticleSurfaceManager manager, int surfaceIndex, Transform surface) {
                _manager = manager;
                _surfaceIndex = surfaceIndex;
                _surface = surface;
                _terrain = surface != null ? surface.GetComponent<Terrain>() : null;
                _showTerrainResolution = _terrain != null;
                _ratioIndex = ClampTerrainResolutionRatio(_terrainResolutionRatioIndex);
            }

            public override Vector2 GetWindowSize() {
                float contentWidth = Mathf.Max(120f, _popupWidth - (_popupPadding * 2f + 2f));
                float messageHeight = GetMaxMessageHeight(contentWidth);
                float height = _popupPadding * 2f + GetContentHeight(messageHeight) + 4f;
                return new Vector2(_popupWidth, Mathf.Ceil(height));
            }

            public override void OnGUI(Rect rect) {
                GUI.Box(rect, GUIContent.none);
                RegenerateMessageInfo message = GetMessageInfo();
                float singleLine = EditorGUIUtility.singleLineHeight;
                Rect contentRect = new Rect(
                    rect.x + _popupPadding + 1f,
                    rect.y + _popupPadding + 1f,
                    Mathf.Max(1f, rect.width - (_popupPadding * 2f + 2f)),
                    Mathf.Max(1f, rect.height - (_popupPadding * 2f + 2f))
                );

                float y = contentRect.y;
                float messageHeight = GetMessageHeight(contentRect.width, message.Text);
                Rect messageRect = new Rect(contentRect.x, y, contentRect.width, messageHeight);
                DrawMessageRow(messageRect, message);
                y += messageHeight + _rowSpacing;

                if (_showTerrainResolution) {
                    Rect terrainRowRect = new Rect(contentRect.x, y, contentRect.width, singleLine);
                    DrawTerrainResolutionRow(terrainRowRect);
                    y += singleLine + _fieldSpacing;

                    Rect polyCountRect = new Rect(contentRect.x, y, contentRect.width, singleLine);
                    DrawTerrainPolyCountRow(polyCountRect);
                    y += singleLine + _fieldSpacing;
                }

                y += _rowSpacing;
                Rect buttonsRect = new Rect(contentRect.x, y, contentRect.width, singleLine + 2f);
                DrawButtons(buttonsRect);
            }

            private float GetContentHeight(float messageHeight) {
                float height = messageHeight;
                height += _rowSpacing;
                if (_showTerrainResolution) {
                    float singleLine = EditorGUIUtility.singleLineHeight;
                    height += singleLine;
                    height += _fieldSpacing;
                    height += singleLine;
                    height += _fieldSpacing;
                }
                height += _rowSpacing;
                height += EditorGUIUtility.singleLineHeight + 2f;
                return height;
            }

            private float GetMaxMessageHeight(float contentWidth) {
                float max = GetMessageHeight(contentWidth, _regenerateMessageNoMesh);
                float h = GetMessageHeight(contentWidth, _regenerateMessageCopyByIndex);
                if (h > max) max = h;
                h = GetMessageHeight(contentWidth, _regenerateMessagePotentialLoss);
                if (h > max) max = h;
                h = GetMessageHeight(contentWidth, _regenerateMessageTerrainSameResolution);
                if (h > max) max = h;
                h = GetMessageHeight(contentWidth, _regenerateMessageTerrainInterpolated);
                if (h > max) max = h;
                h = GetMessageHeight(contentWidth, _regenerateMessageTerrainCannotInterpolate);
                if (h > max) max = h;
                h = GetMessageHeight(contentWidth, _regenerateMessageCopyByIndexReadonly);
                if (h > max) max = h;
                h = GetMessageHeight(contentWidth, _regenerateMessageTerrainSameResolutionReadonly);
                if (h > max) max = h;
                return max;
            }

            private float GetMessageHeight(float contentWidth, string messageText) {
                float textWidth = Mathf.Max(100f, contentWidth - _iconSize - _iconGap);
                return Mathf.Max(_iconSize, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(messageText), textWidth)) + 2f;
            }

            private RegenerateMessageInfo GetMessageInfo() {
                Mesh previousMesh = GetAssignedSurfaceMesh(_manager, _surfaceIndex);
                if (previousMesh == null) {
                    return new RegenerateMessageInfo {
                        Text = _regenerateMessageNoMesh,
                        IsWarning = false
                    };
                }

                int plannedVertexCount = GetPlannedSurfaceMeshVertexCount(_surface, _ratioIndex);
                if (plannedVertexCount <= 0) {
                    return new RegenerateMessageInfo {
                        Text = _regenerateMessagePotentialLoss,
                        IsWarning = true
                    };
                }

                bool sameVertexCount = previousMesh.vertexCount == plannedVertexCount;
                if (_showTerrainResolution) {
                    if (sameVertexCount) {
                        return new RegenerateMessageInfo {
                            Text = IsMeshEditable(previousMesh)
                                ? _regenerateMessageTerrainSameResolution
                                : _regenerateMessageTerrainSameResolutionReadonly,
                            IsWarning = false
                        };
                    }

                    int previousResolution;
                    if (TryGetSquareGridResolution(previousMesh.vertexCount, out previousResolution)) {
                        return new RegenerateMessageInfo {
                            Text = _regenerateMessageTerrainInterpolated,
                            IsWarning = true
                        };
                    }

                    return new RegenerateMessageInfo {
                        Text = _regenerateMessageTerrainCannotInterpolate,
                        IsWarning = true
                    };
                }

                if (sameVertexCount) {
                    return new RegenerateMessageInfo {
                        Text = IsMeshEditable(previousMesh)
                            ? _regenerateMessageCopyByIndex
                            : _regenerateMessageCopyByIndexReadonly,
                        IsWarning = false
                    };
                }

                return new RegenerateMessageInfo {
                    Text = _regenerateMessagePotentialLoss,
                    IsWarning = true
                };
            }

            private void DrawMessageRow(Rect rect, RegenerateMessageInfo message) {
                Rect iconRect = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - _iconSize) * 0.5f), _iconSize, _iconSize);
                Rect textRect = new Rect(iconRect.xMax + _iconGap, rect.y, Mathf.Max(1f, rect.width - _iconSize - _iconGap), rect.height);
                Color previousColor = GUI.color;
                GUIContent icon = message.IsWarning ? GetWarningIconContent() : GetInfoIconContent();
                if (message.IsWarning) GUI.color = new Color(1f, 0.85f, 0.2f, 1f);
                GUI.Label(iconRect, icon);
                GUI.color = previousColor;
                GUI.Label(textRect, message.Text, EditorStyles.wordWrappedLabel);
            }

            private void DrawTerrainResolutionRow(Rect rowRect) {
                Rect labelRect = rowRect;
                labelRect.width = Mathf.Min(116f, rowRect.width * 0.45f);
                Rect popupRect = rowRect;
                popupRect.xMin = labelRect.xMax + 4f;
                int next = EditorGUI.Popup(popupRect, _ratioIndex, _terrainResolutionRatioLabels);
                EditorGUI.LabelField(labelRect, _terrainResolutionPopupLabelContent);
                if (next != _ratioIndex) _ratioIndex = next;
            }

            private void DrawTerrainPolyCountRow(Rect rect) {
                int terrainResolution = GetConfiguredTerrainResolution(_terrain, _ratioIndex);
                int quads = Mathf.Max(1, terrainResolution - 1);
                long triangles = (long)quads * quads * 2L;
                EditorGUI.LabelField(rect, _terrainPolyCountLabelPrefix + ": " + triangles.ToString("N0"));
            }

            private void DrawButtons(Rect rect) {
                float buttonWidth = Mathf.Max(1f, (rect.width - _buttonSpacing) * 0.5f);
                Rect regenerateRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
                Rect cancelRect = new Rect(regenerateRect.xMax + _buttonSpacing, rect.y, buttonWidth, rect.height);
                bool regenerate = GUI.Button(regenerateRect, _regenerateConfirmButtonLabel);
                bool cancel = GUI.Button(cancelRect, _cancelButtonLabel);
                if (regenerate) {
                    Regenerate();
                    return;
                }
                if (cancel) {
                    ClosePopup();
                    return;
                }
            }

            private void Regenerate() {
                if (_showTerrainResolution) {
                    SetTerrainResolutionRatio(_ratioIndex);
                }

                Mesh previousMesh = GetAssignedSurfaceMesh(_manager, _surfaceIndex);
                Mesh regeneratedMesh = CreatePreparedSurfaceMesh(_surface);
                if (regeneratedMesh == null) {
                    ClosePopup();
                    return;
                }

                bool canOverwriteInPlace = CanRegenerateWithoutPaintLoss(previousMesh, regeneratedMesh);
                TransferPaintMask(previousMesh, regeneratedMesh, _showTerrainResolution && !canOverwriteInPlace);

                if (canOverwriteInPlace) {
                    OverwriteMeshAssetInPlace(previousMesh, regeneratedMesh);
                    _touchedMeshes.Add(previousMesh);
                    DestroyImmediate(regeneratedMesh);
                } else {
                    Mesh createdAssetMesh = CreateSurfaceMeshAssetFromPrepared(_surface, regeneratedMesh);
                    if (createdAssetMesh != null) {
                        _touchedMeshes.Add(createdAssetMesh);
                        AssignSurfaceMesh(_manager, _surfaceIndex, createdAssetMesh);
                    } else {
                        DestroyImmediate(regeneratedMesh);
                    }
                }

                ClearGeometryCaches();
                SceneView.RepaintAll();
                ClosePopup();
            }

            private void ClosePopup() {
                if (editorWindow != null) editorWindow.Close();
                GUIUtility.ExitGUI();
            }
        }
    }
}
