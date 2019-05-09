using System;
using System.Windows;
using System.Windows.Media;

namespace SketchToAI
{
    public static class UIHelpers
    {
        public static T FindChild<T>(this DependencyObject root, string name = null)
            where T : DependencyObject
        {    
            if (root == null) throw new ArgumentNullException(nameof(root));
            T foundChild = null;
            var childrenCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(root, i);
                var typedChild = child as T;
                if (typedChild == null) {
                    foundChild = FindChild<T>(child, name);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(name)) {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == name) {
                        foundChild = (T) child;
                        break;
                    }
                }
                else {
                    foundChild = (T)child;
                    break;
                }
            }
            return foundChild;
        }        
    }
}
