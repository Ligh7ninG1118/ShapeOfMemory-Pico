using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // Gym inspector: preview buttons, live stats, the 64 x 64 image the game scores per wall, how the previewed orb
    // rotation scores against the answer, and check / export / play / open.
    [CustomEditor(typeof(Torch2Gym))]
    public sealed class Torch2GymEditor : UnityEditor.Editor
    {
        Torch2Level open;
        readonly List<Texture2D> images=new List<Texture2D>();
        Torch2Projection imagesOf,scoredOf;
        Quaternion scoredAt;
        float scoredMin;
        string scored;
        public override bool RequiresConstantRepaint()=>true;
        void OnEnable()=>EditorApplication.QueuePlayerLoopUpdate();
        void OnDisable(){foreach(var t in images)DestroyImmediate(t);images.Clear();imagesOf=null;}
        public override void OnInspectorGUI()
        {
            var gym=(Torch2Gym)target;
            serializedObject.Update();DrawPropertiesExcluding(serializedObject,"m_Script");serializedObject.ApplyModifiedProperties();
            if(!gym.cluster){EditorGUILayout.HelpBox("Assign the cluster: a child of the gym whose children are the pieces.",MessageType.Warning);return;}

            Header("Preview (no Play mode needed)");
            Row(("Show answer",()=>Turn(gym,Quaternion.identity)),("Show start",()=>Turn(gym,gym.start)),("Use orb rotation as start",()=>{Undo.RecordObject(gym,"Set start");gym.start=gym.cluster.localRotation;gym.autoStart=false;}));
            Row(("Look from left lamp",()=>Torch2GymTools.LookFromLamp(gym,0)),("Look from right lamp",()=>Torch2GymTools.LookFromLamp(gym,1)));
            Row(("Recenter pieces",()=>Torch2GymTools.Recenter(gym)),("Fit inside orb",()=>Torch2GymTools.FitInsideOrb(gym)));
            Row(("Selection: visual only",()=>Torch2GymTools.Mark(ShadowPiece.Role.VisualOnly)),("Selection: picture",()=>Torch2GymTools.Mark(ShadowPiece.Role.Picture)));
            int picture=0;foreach(var p in gym.Pieces)if(!p.visualOnly)picture++;
            int triangles=Torch2GymTools.ShadowTriangles(gym);float radius=Torch2GymTools.Radius(gym);
            EditorGUILayout.LabelField($"{picture} picture pieces, {gym.Pieces.Count-picture} visual only");
            EditorGUILayout.LabelField($"{triangles} shadow triangles of {Torch2Gym.ShadowTriangleBudget}; furthest piece {radius*100:0} cm of {Torch2Gym.MaxPieceRadius*100:0} cm");
            if(triangles>Torch2Gym.ShadowTriangleBudget||radius>Torch2Gym.MaxPieceRadius)EditorGUILayout.HelpBox("Over budget or outside the orb; Check level explains.",MessageType.Warning);
            if(gym.Notes.Trim()!="")EditorGUILayout.HelpBox(gym.Notes.Trim(),MessageType.Warning);

            var projection=gym.Projection;
            if(projection!=null)
            {
                if(projection!=imagesOf)Images(projection,gym.accent);
                EditorGUILayout.LabelField("What the game scores at the answer (64 x 64, position and size removed)",EditorStyles.miniLabel);
                using(new EditorGUILayout.HorizontalScope())
                {
                    for(int j=0;j<images.Count;j++)
                    {
                        using(new EditorGUILayout.VerticalScope(GUILayout.Width(132)))
                        {
                            EditorGUILayout.LabelField(projection.walls[j]==0?"Left wall":"Right wall",GUILayout.Width(128));
                            var rect=GUILayoutUtility.GetRect(128,128,GUILayout.ExpandWidth(false));GUI.DrawTexture(rect,images[j]);
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
                var rotation=gym.cluster.localRotation;
                if(Quaternion.Angle(rotation,Quaternion.identity)>.01f)
                {
                    if(projection!=scoredOf||!rotation.Equals(scoredAt)||gym.iouMin!=scoredMin)
                    {
                        scoredOf=projection;scoredAt=rotation;scoredMin=gym.iouMin;projection.level.iouMin=gym.iouMin;
                        projection.Render(rotation,Torch2Projection.Home);projection.Evaluate();var lines=new List<string>();
                        for(int j=0;j<projection.walls.Length;j++){var s=projection.scores[j];lines.Add($"{(projection.walls[j]==0?"Left":"Right")} wall: IoU {s.iou:0.00}, cover {s.cover:0.00}, spill {s.spill:0.00}{(s.Ready?", passes":"")}");}
                        scored=$"Orb turned {Quaternion.Angle(rotation,Quaternion.identity):0.0} degrees from the answer (threshold IoU {gym.iouMin:0.00}).\n"+string.Join("\n",lines);
                    }
                    EditorGUILayout.HelpBox(scored,MessageType.Info);
                }
            }

            Header("Saved views (orb rotations to come back to)");
            Views(gym);

            Header("Table to orb");
            if(!gym.tableLayout)EditorGUILayout.LabelField("No table layout: the things are scattered on the table, as the game does by itself.",EditorStyles.wordWrappedMiniLabel);
            if(Application.isPlaying)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    using(new EditorGUI.DisabledScope(gym.Transiting))if(GUILayout.Button("Replay table to orb"))gym.PlayTransit();
                    using(new EditorGUI.DisabledScope(!gym.Transiting))if(GUILayout.Button("Skip intro"))gym.SkipTransit();
                }
                if(gym.Transiting)EditorGUILayout.LabelField($"Playing, {gym.TransitLength:0.0} s (Skip intro here, in the Game view or Space skips; untick Play Intro to never see it).",EditorStyles.wordWrappedMiniLabel);
                else EditorGUILayout.LabelField("In the Game view: drag rolls the orb, A/D W/S Z/C turn it, the wheel moves the camera nearer or further, right-drag pans it, V saves the view.",EditorStyles.wordWrappedMiniLabel);
            }
            else EditorGUILayout.LabelField("Enter Play mode to watch the things lift from the table into the orb, the rings close and the lamps come on, as in the game's intro; then roll the orb with the mouse. Check level looks for clashes on the way.",EditorStyles.wordWrappedMiniLabel);

            Header("Level");
            Row(("Check level",()=>Torch2GymTools.Check(gym)),("Export",()=>Torch2GymTools.Export(gym)),("Play this level",()=>Torch2GymTools.Play(gym)));
            if(Torch2GymTools.RunOnHeadset!=null&&!Application.isPlaying)Row(("Run on headset",()=>Torch2GymTools.RunOnHeadset(gym)));
            using(new EditorGUILayout.HorizontalScope())
            {
                open=(Torch2Level)EditorGUILayout.ObjectField("Open level",open,typeof(Torch2Level),false);
                using(new EditorGUI.DisabledScope(!open))if(GUILayout.Button("Open in gym",GUILayout.Width(100))){var level=open;EditorApplication.delayCall+=()=>Torch2GymTools.Load(gym,level);}
            }
            if(!string.IsNullOrEmpty(gym.report))EditorGUILayout.HelpBox(gym.report,MessageType.None);
        }
        // One row per saved view (name, how far from the answer, Show, Delete), Save and Clear; while a view turns the orb
        // in edit mode, the way back to the rotation it had before.
        static void Views(Torch2Gym gym)
        {
            var before=Torch2Gym.Rotation(gym.beforeViews);
            if(gym.showingView&&!Application.isPlaying&&Quaternion.Angle(gym.cluster.localRotation,before)>.01f)
            {
                string was=Quaternion.Angle(before,Quaternion.identity)<.01f?"the answer":$"{gym.AngleFromAnswer(before):0} degrees from the answer";
                EditorGUILayout.HelpBox($"The orb is turned to preview views; before, it was at {was}. Only the orb turned: the pieces, which are the answer, did not move, so nothing of the level is lost. The walls show this view, not the answer.",MessageType.Info);
                Row(("Back to original rotation",()=>Torch2GymTools.BackToOriginal(gym)),("Keep this rotation",()=>Torch2GymTools.KeepRotation(gym)));
            }
            int shown=gym.ShownView();
            for(int i=0;i<gym.views.Count;i++)
            {
                int k=i;var view=gym.views[i];
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();var name=EditorGUILayout.DelayedTextField(view.name,GUILayout.MinWidth(60));
                    if(EditorGUI.EndChangeCheck())EditorApplication.delayCall+=()=>Torch2GymTools.RenameView(gym,k,name);
                    EditorGUILayout.LabelField($"{gym.AngleFromAnswer(view.rotation):0} deg from answer{(k==shown?", shown":"")}",EditorStyles.miniLabel,GUILayout.Width(150));
                    using(new EditorGUI.DisabledScope(gym.Transiting||k==shown))if(GUILayout.Button("Show",GUILayout.Width(48)))EditorApplication.delayCall+=()=>Torch2GymTools.ShowView(gym,k);
                    if(GUILayout.Button("Delete",GUILayout.Width(52)))EditorApplication.delayCall+=()=>Torch2GymTools.DeleteView(gym,k);
                }
            }
            if(gym.views.Count==0)EditorGUILayout.LabelField(Application.isPlaying?"None yet: roll the orb in the Game view and press V, or press Save below.":"None yet: in Play mode roll the orb and press V, or save the orb's rotation here.",EditorStyles.wordWrappedMiniLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(gym.Transiting))if(GUILayout.Button("Save orb rotation as a view"))EditorApplication.delayCall+=()=>Torch2GymTools.SaveView(gym);
                using(new EditorGUI.DisabledScope(gym.views.Count==0))if(GUILayout.Button("Clear saved views..."))EditorApplication.delayCall+=()=>Torch2GymTools.ClearViews(gym);
            }
            if(Application.isPlaying)EditorGUILayout.LabelField("Views saved, renamed or deleted in Play mode stay when it ends. The orb itself goes back to its edit-mode rotation.",EditorStyles.wordWrappedMiniLabel);
        }
        static void Header(string text){EditorGUILayout.Space();EditorGUILayout.LabelField(text,EditorStyles.boldLabel);}
        // Buttons run after the inspector has drawn, since they may open dialogs, progress bars or other scenes.
        static void Row(params (string label,Action action)[] buttons)
        {
            using(new EditorGUILayout.HorizontalScope())foreach(var (label,action) in buttons)if(GUILayout.Button(label))EditorApplication.delayCall+=()=>action();
        }
        static void Turn(Torch2Gym gym,Quaternion rotation){Undo.RecordObject(gym.cluster,"Preview orb");gym.cluster.localRotation=rotation;}
        void Images(Torch2Projection projection,Color accent)
        {
            foreach(var t in images)DestroyImmediate(t);images.Clear();imagesOf=projection;int g=Torch2Projection.Grid;
            for(int j=0;j<projection.walls.Length;j++)
            {
                var texture=new Texture2D(g,g,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,hideFlags=HideFlags.DontSave};var pixels=new Color32[g*g];
                for(int i=0;i<pixels.Length;i++)pixels[i]=projection.target[j][i]?Color.Lerp(accent,Torch2Room.Dark,.55f):new Color(.93f,.88f,.78f);
                texture.SetPixels32(pixels);texture.Apply(false,false);images.Add(texture);
            }
        }
    }
}
