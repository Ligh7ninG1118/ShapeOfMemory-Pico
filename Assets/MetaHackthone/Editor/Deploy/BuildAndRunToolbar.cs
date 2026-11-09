using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // Next to Play: what Build and Run will build and where, and the button that does it.
    static class BuildAndRunToolbar
    {
        const string Path="MetaHackthone/Build and Run";
        public static void Refresh()=>MainToolbar.Refresh(Path);
        [InitializeOnLoadMethod] static void Watch()=>BuildAndRun.Changed+=Refresh;

        [MainToolbarElement(Path,defaultDockPosition=MainToolbarDockPosition.Middle)]
        static IEnumerable<MainToolbarElement> Create()
        {
            var config=BuildAndRun.Config; var headset=config?BuildAndRun.Target(config):null;
            string where=headset!=null?headset.Name:BuildAndRun.Serial==BuildAndRun.NoHeadset?$"{BuildAndRun.PlatformFor(config,null)} build only":"no headset";
            yield return new MainToolbarDropdown(new MainToolbarContent(config?$"{config.name} · {where}":"No build config","The game Build and Run builds, and the headset it runs on"),ShowMenu);
            yield return new MainToolbarButton(new MainToolbarContent("Build and Run","Build it, install it on the headset and start it"),()=>EditorApplication.delayCall+=BuildAndRun.BuildAndRunNow);
        }
        static void ShowMenu(Rect rect)
        {
            Headsets.Refresh();
            var menu=new GenericMenu();
            foreach(var config in DeployConfig.All()) { var c=config; menu.AddItem(new GUIContent("Game/"+c.name),c==BuildAndRun.Config,()=>BuildAndRun.Config=c); }
            menu.AddItem(new GUIContent("Headset/Any headset"),BuildAndRun.Serial==BuildAndRun.AnyHeadset,()=>BuildAndRun.Serial=BuildAndRun.AnyHeadset);
            foreach(var headset in Headsets.Connected) { var serial=headset.Serial; menu.AddItem(new GUIContent("Headset/"+headset.Label),BuildAndRun.Serial==serial,()=>BuildAndRun.Serial=serial); }
            menu.AddItem(new GUIContent("Headset/No headset (build only)"),BuildAndRun.Serial==BuildAndRun.NoHeadset,()=>BuildAndRun.Serial=BuildAndRun.NoHeadset);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Development build"),BuildAndRun.Development,()=>BuildAndRun.Development=!BuildAndRun.Development);
            menu.AddItem(new GUIContent("Open Build and Run window"),false,BuildAndRunWindow.Open);
            menu.DropDown(rect);
            Refresh();
        }
    }
}
