using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public static class TabBarHelperTool
    {
        public static void SetVisibility(bool isVisible)
        {
            var tabContext = Shell.Current?.CurrentItem?.CurrentItem;

            if (tabContext != null)
                Shell.SetTabBarIsVisible(tabContext, isVisible);
        }
    }

}
