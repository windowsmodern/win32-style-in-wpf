using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace System.Windows.Controls
{
	/// <summary>
	/// 把 Content（ButtonBase）的悬停 / 按下 / 键盘焦点状态接到宿主按钮的选图逻辑上。
	/// 只是读取状态，不改变输入路由：子按钮继续接收 Click。
	/// </summary>
	internal static class HackButtonContentState
	{
		private static readonly DependencyPropertyDescriptor MouseOver =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsMouseOverProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor KeyboardFocus =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsKeyboardFocusedProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor Pressed =
			DependencyPropertyDescriptor.FromProperty (ButtonBase.IsPressedProperty, typeof (ButtonBase));

		internal static void Attach (ButtonBase content, EventHandler handler)
		{
			MouseOver.AddValueChanged (content, handler);
			KeyboardFocus.AddValueChanged (content, handler);
			Pressed.AddValueChanged (content, handler);
		}

		internal static void Detach (ButtonBase content, EventHandler handler)
		{
			MouseOver.RemoveValueChanged (content, handler);
			KeyboardFocus.RemoveValueChanged (content, handler);
			Pressed.RemoveValueChanged (content, handler);
		}
	}
}
