using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UdonSharpEditor;
using VRC.SDKBase;

namespace ArchiTech.Editor
{
    [CustomEditor(typeof(SimplePlaylist))]
    public class SimplePlaylistEditor : UnityEditor.Editor
    {
        SimplePlaylist playlist;
        TVManagerV2 tv;
        RectTransform listContainer;
        GameObject template;
        bool autoplayList;
        bool continueWhereLeftOff;
        bool autoplayOnVideoError;
        bool showUrls = true;
        VRCUrl[] urls;
        VRCUrl[] oldUrls;
        string[] titles;
        string[] oldTitles;
        Vector2 scrollPos;

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            playlist = (SimplePlaylist)target;

            oldUrls = playlist.urls;
            oldTitles = playlist.titles;
            if (oldUrls == null) oldUrls = new VRCUrl[0];
            if (oldTitles == null) oldTitles = new string[0];
            urls = new VRCUrl[oldUrls.Length];
            titles = new string[oldTitles.Length];
            Array.Copy(oldUrls, urls, oldUrls.Length);
            Array.Copy(oldTitles, titles, oldTitles.Length);

            EditorGUI.BeginChangeCheck();
            showPlaylistProperties();
            showPlaylistControls();
            showPlaylistEntries();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(playlist, "Modify Playlist Content");
                playlist.tv = tv;
                playlist.listContainer = listContainer;
                playlist.template = template;
                playlist.autoplayList = autoplayList;
                playlist.continueWhereLeftOff = continueWhereLeftOff;
                playlist.autoplayOnVideoError = autoplayOnVideoError;
                playlist.urls = urls;
                playlist.titles = titles;
                playlist.showUrls = showUrls;
                updateSceneEntries(); 
            }
        }

        private void showPlaylistProperties()
        {
            EditorGUILayout.Space();

            tv = (TVManagerV2)EditorGUILayout.ObjectField("TV", playlist.tv, typeof(TVManagerV2), true);
            listContainer = (RectTransform)EditorGUILayout.ObjectField("Playlist Item Container", playlist.listContainer, typeof(RectTransform), true);
            template = (GameObject)EditorGUILayout.ObjectField("Playlist Item Template", playlist.template, typeof(GameObject), true);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Autoplay?");
            autoplayList = EditorGUILayout.Toggle(playlist.autoplayList);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(!autoplayList);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.PrefixLabel("Continue from where it left off:");
            continueWhereLeftOff = EditorGUILayout.Toggle(playlist.continueWhereLeftOff);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.PrefixLabel("Continue after a video error:");
            autoplayOnVideoError = EditorGUILayout.Toggle(playlist.autoplayOnVideoError);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Show urls in playlist?");
            showUrls = EditorGUILayout.Toggle(playlist.showUrls);
            EditorGUILayout.EndHorizontal();
        }

        private void showPlaylistControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Video Playlist Items");

            GUILayout.Button("Refresh", GUILayout.MaxWidth(60f));

            EditorGUI.BeginDisabledGroup(oldUrls.Length == 100);
            if (GUILayout.Button(oldUrls.Length < 100 ? "Add Entry" : "Max 100 Items"))
                addPlaylistEntry();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(oldUrls.Length == 0);
            if (GUILayout.Button("Remove All", GUILayout.MaxWidth(100f)))
            {
                Debug.Log($"Removing all {oldUrls.Length} playlist items");
                urls = new VRCUrl[0];
                titles = new string[0];
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void showPlaylistEntries()
        {
            EditorGUILayout.Space();
            var height = Mathf.Min(200f, urls.Length * 50f);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(height));
            for (var i = 0; i < urls.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                // URL field management
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Video {i + 1} Url", GUILayout.MaxWidth(100f));
                var url = new VRCUrl(EditorGUILayout.TextField(urls[i].Get(), GUILayout.ExpandWidth(true)));
                // if (urls[i].Get() != url.Get()) forceChange = true;
                urls[i] = url;

                EditorGUILayout.EndHorizontal();

                // TITLE field management
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Display Title", GUILayout.MaxWidth(100f));
                var title = EditorGUILayout.TextArea(titles[i], GUILayout.MaxWidth(250f));
                if (title != null && title.Length > 140) title = title.Substring(0, 140);
                // if (titles[i] != title) forceChange = true;
                titles[i] = title;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // Playlist entry actions
                EditorGUILayout.BeginVertical();
                if (GUILayout.Button("Remove"))
                    removePlaylistEntry(i);

                // Playlist entry ordering
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(i == 0);
                if (GUILayout.Button("Up"))
                    movePlaylistEntry(i, i - 1);
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(i + 1 == urls.Length);
                if (GUILayout.Button("Down"))
                    movePlaylistEntry(i, i + 1);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }
            EditorGUILayout.EndScrollView();
        }

        private void addPlaylistEntry()
        {
            Debug.Log($"Adding playlist item {playlist.urls.Length}");
            urls = new VRCUrl[oldUrls.Length + 1];
            titles = new string[oldTitles.Length + 1];
            int i = 0;
            for (; i < playlist.urls.Length; i++)
            {
                urls[i] = playlist.urls[i];
                titles[i] = playlist.titles[i];
            }
            urls[i] = VRCUrl.Empty;
        }

        private void removePlaylistEntry(int index)
        {
            Debug.Log($"Removing playlist item {index}");
            urls = new VRCUrl[oldUrls.Length - 1];
            titles = new string[playlist.titles.Length - 1];
            int offset = 0;
            for (int i = 0; i < urls.Length; i++)
            {
                if (i == index)
                {
                    offset = 1;
                    continue;
                }
                urls[i] = playlist.urls[i + offset];
                titles[i] = playlist.titles[i + offset];
            }
        }

        private void movePlaylistEntry(int from, int to)
        {
            // no change needed
            if (from == to) return;
            Debug.Log($"Moving playlist item {from} -> {to}");
            // cache the source index
            var fromUrl = urls[from];
            var fromTitle = titles[from];
            // determines the direction to shift
            int direction = from < to ? 1 : -1;
            // calculate the actual start and end values for the loop
            int start = Math.Min(from, to);
            int end = start + Math.Abs(to - from);
            for (int i = start; i <= end; i++)
            {
                // don't assign the target values yet
                if (i == to) continue;
                urls[i] = urls[i + direction];
                titles[i] = titles[i + direction];
            }
            // assign the target values now
            urls[to] = fromUrl;
            titles[to] = fromTitle;
        }

        private void updateSceneEntries()
        {
            if (playlist.template == null || playlist.listContainer == null) return; // must have both to generate UI list.
            GameObject tmpl = playlist.template;
            RectTransform list = playlist.listContainer;
            var tmplRect = tmpl.GetComponent<RectTransform>().rect;
            var playlistEvents = UdonSharpEditorUtility.GetBackingUdonBehaviour(playlist);
            while (list.childCount > 0)
                Undo.DestroyObjectImmediate(list.GetChild(0).gameObject);
            Button[] btns = new Button[playlist.urls.Length];
            byte visibleCount = 0;
            for (int i = 0; i < playlist.urls.Length; i++)
            {
                if (playlist.urls[i].Get() == string.Empty) continue;
                GameObject entry;
                entry = Instantiate(tmpl, list, false);
                Undo.RegisterCreatedObjectUndo(entry, "Create Playlist Item In Scene");
                entry.transform.localPosition = new Vector3(0f, -(tmplRect.height * visibleCount + tmplRect.height * 0.5f));
                entry.name = $"Entry ({i})";
                entry.SetActive(true);

                // set UI event for the button
                var button = entry.GetComponentInChildren<Button>();
                UnityAction<string> action = new UnityAction<string>(playlistEvents.SendCustomEvent);
                UnityEventTools.AddStringPersistentListener(button.onClick, action, nameof(playlist._SwitchTo) + i);
                // set entry title
                var title = entry.transform.Find("Title");
                if (title != null) title.GetComponent<Text>().text = playlist.titles[i];
                // set entry url
                var url = entry.transform.Find("Url");
                if (showUrls && url != null) url.GetComponent<Text>().text = playlist.urls[i].Get();
                visibleCount++;
            }
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, tmplRect.height * visibleCount + tmplRect.height * 0.5f);
        }
    }
}