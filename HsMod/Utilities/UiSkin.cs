using System;
using System.Collections.Generic;
using UnityEngine;

namespace HsMod
{


	public sealed class UiSkin
	{
		private enum Corner
		{
			TopLeft,
			TopRight,
			BottomLeft,
			BottomRight
		}

		private sealed class RoundVariant
		{
			public int RadiusPx;

			public Color Fill;

			public Texture2D[] Corners;

			public Texture2D[] Rings;

			public int RingThicknessPx;
		}

		public static readonly Color ColBg;

		public static readonly Color ColSurface;

		public static readonly Color ColRaised;

		public static readonly Color ColInput;

		public static readonly Color ColBorder;

		public static readonly Color ColLine;

		public static readonly Color ColText;

		public static readonly Color ColMuted;

		public static readonly Color ColFaint;

		public static readonly Color ColAccent;

		public static readonly Color ColAccentSoft;

		public static readonly Color ColAccentLine;

		public static readonly Color ColGreen;

		public static readonly Color ColRed;

		public static readonly Color ColHoverRaised;

		public static readonly Color ColHoverBorder;

		public static readonly Color ColOverlay;

		public static readonly Color ColGrid;

		public static readonly Color ColShadowOuter;

		public static readonly Color ColShadowInner;

		public static readonly Color ColPanel;

		public static readonly Color ColPanelShadow;

		public static readonly Color ColTrackOff;

		public static readonly Color ColKnob;

		public static readonly Color ColKnobOn;

		public static readonly Color ColKnobShadow;

		public static readonly Color ColFocus;

		public static readonly Color ColPressed;

		public const float RadiusWindow = 14f;

		public const float RadiusCard = 12f;

		public const float RadiusControl = 8f;

		private const float TextureSuperSample = 1.5f;

		private float assetsScale;

		private int assetsScaleBucket = int.MinValue;

		private List<Texture2D> uiTextures = new List<Texture2D>();

		private readonly List<Texture2D> pendingDestroy = new List<Texture2D>();

		private readonly List<int> pendingDestroyFrames = new List<int>();

		private readonly Dictionary<Color, Texture2D> solidTextures = new Dictionary<Color, Texture2D>();

		private readonly Dictionary<Color, Texture2D> circleTextures = new Dictionary<Color, Texture2D>();

		private readonly List<RoundVariant> roundVariants = new List<RoundVariant>();

		private readonly Dictionary<long, RoundVariant> roundVariantIndex = new Dictionary<long, RoundVariant>();

		public void Bind(List<Texture2D> ownedTextures)
		{
			uiTextures = ownedTextures;
		}

		public static float MeasureDevicePixelsPerPoint()
		{
			try
			{
				int num;
				int num2;
				GUIUtility.AlignRectToDevice(new Rect(0f, 0f, 100f, 100f), out num, out num2);
				if (num > 0)
				{
					return Mathf.Clamp((float)num / 100f, 1f, 4f);
				}
			}
			catch
			{
			}
			return 1f;
		}

		private static float SnapToDevicePixel(float value, float p)
		{
			if (!(p <= 1f))
			{
				return Mathf.Round(value * p) / p;
			}
			return Mathf.Round(value);
		}

		private static Rect SnapToDevicePixelRect(Rect r, float p)
		{
			float num = SnapToDevicePixel(r.x, p);
			float num2 = SnapToDevicePixel(r.y, p);
			float num3 = SnapToDevicePixel(r.xMax, p);
			float num4 = SnapToDevicePixel(r.yMax, p);
			return new Rect(num, num2, num3 - num, num4 - num2);
		}

		public static int ScaleBucket(float scale)
		{
			return Mathf.RoundToInt(scale * 4f);
		}

		public bool EnsureScale(float layoutScale)
		{
			if (Event.current != null && (int)Event.current.type == 8)
			{
				return false;
			}
			FlushRetiredTextures();
			float scale = layoutScale * MeasureDevicePixelsPerPoint() * 1.5f;
			int num = ScaleBucket(scale);
			if (assetsScaleBucket == num)
			{
				return false;
			}
			RetireTextures();
			assetsScale = scale;
			assetsScaleBucket = num;
			return true;
		}

		private void RetireTextures()
		{
			foreach (Texture2D uiTexture in uiTextures)
			{
				if (!(uiTexture == null))
				{
					pendingDestroy.Add(uiTexture);
					pendingDestroyFrames.Add(Time.frameCount);
				}
			}
			uiTextures.Clear();
			solidTextures.Clear();
			circleTextures.Clear();
			roundVariants.Clear();
			roundVariantIndex.Clear();
		}

		private void FlushRetiredTextures()
		{
			if (pendingDestroy.Count == 0)
			{
				return;
			}
			int frameCount = Time.frameCount;
			for (int num = pendingDestroy.Count - 1; num >= 0; num--)
			{
				if (frameCount - pendingDestroyFrames[num] >= 2)
				{
					if (pendingDestroy[num] != null)
					{
						UnityEngine.Object.Destroy(pendingDestroy[num]);
					}
					pendingDestroy.RemoveAt(num);
					pendingDestroyFrames.RemoveAt(num);
				}
			}
		}

		public void ReleaseTextures()
		{
			foreach (Texture2D uiTexture in uiTextures)
			{
				if (uiTexture != null)
				{
					UnityEngine.Object.Destroy(uiTexture);
				}
			}
			uiTextures.Clear();
			solidTextures.Clear();
			circleTextures.Clear();
			roundVariants.Clear();
			roundVariantIndex.Clear();
			assetsScaleBucket = int.MinValue;
			foreach (Texture2D item in pendingDestroy)
			{
				if (item != null)
				{
					UnityEngine.Object.Destroy(item);
				}
			}
			pendingDestroy.Clear();
			pendingDestroyFrames.Clear();
		}

		public Texture2D Tex(Color color)
		{
			if (solidTextures.TryGetValue(color, out var value) && value != null)
			{
				return value;
			}
			value = new Texture2D(1, 1, (TextureFormat)4, false);
			((Texture)value).filterMode = (FilterMode)0;
			((Texture)value).wrapMode = (TextureWrapMode)1;
			value.SetPixel(0, 0, color);
			value.Apply();
			(value).hideFlags = (HideFlags)61;
			solidTextures[color] = value;
			uiTextures.Add(value);
			return value;
		}

		public Texture2D CircleTexture(float diameterUnits, Color fill)
		{
			if (circleTextures.TryGetValue(fill, out var value) && value != null)
			{
				return value;
			}
			int num = Mathf.Clamp(Mathf.RoundToInt(diameterUnits * assetsScale), 6, 96);
			Texture2D val = new Texture2D(num, num, (TextureFormat)4, false);
			((Texture)val).filterMode = (FilterMode)1;
			((Texture)val).wrapMode = (TextureWrapMode)1;
			(val).hideFlags = (HideFlags)61;
			float num2 = (float)num * 0.5f;
			Color[] array = (Color[])(object)new Color[num * num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < num; j++)
				{
					float num3 = (float)j + 0.5f - num2;
					float num4 = (float)i + 0.5f - num2;
					float num5 = Mathf.Clamp01(num2 - Mathf.Sqrt(num3 * num3 + num4 * num4) + 0.5f);
					Color val2 = fill;
					val2.a = fill.a * num5;
					array[i * num + j] = val2;
				}
			}
			val.SetPixels(array);
			val.Apply();
			circleTextures[fill] = val;
			uiTextures.Add(val);
			return val;
		}

		private Texture2D MakeCornerTexture(int px, Color fill, Corner corner)
		{
			Texture2D val = new Texture2D(px, px, (TextureFormat)4, false);
			((Texture)val).filterMode = (FilterMode)1;
			((Texture)val).wrapMode = (TextureWrapMode)1;
			(val).hideFlags = (HideFlags)61;
			float num;
			float num2;
			switch (corner)
			{
			case Corner.TopLeft:
				num = px;
				num2 = 0f;
				break;
			case Corner.TopRight:
				num = 0f;
				num2 = 0f;
				break;
			case Corner.BottomLeft:
				num = px;
				num2 = px;
				break;
			default:
				num = 0f;
				num2 = px;
				break;
			}
			Color[] array = (Color[])(object)new Color[px * px];
			for (int i = 0; i < px; i++)
			{
				for (int j = 0; j < px; j++)
				{
					float num3 = (float)j + 0.5f - num;
					float num4 = (float)i + 0.5f - num2;
					float num5 = Mathf.Clamp01((float)px - Mathf.Sqrt(num3 * num3 + num4 * num4) + 0.5f);
					Color val2 = fill;
					val2.a = fill.a * num5;
					array[i * px + j] = val2;
				}
			}
			val.SetPixels(array);
			val.Apply();
			return val;
		}

		private Texture2D MakeRingCornerTexture(int px, Color fill, Corner corner, int thicknessPx)
		{
			Texture2D val = new Texture2D(px, px, (TextureFormat)4, false);
			((Texture)val).filterMode = (FilterMode)1;
			((Texture)val).wrapMode = (TextureWrapMode)1;
			(val).hideFlags = (HideFlags)61;
			float num;
			float num2;
			switch (corner)
			{
			case Corner.TopLeft:
				num = px;
				num2 = 0f;
				break;
			case Corner.TopRight:
				num = 0f;
				num2 = 0f;
				break;
			case Corner.BottomLeft:
				num = px;
				num2 = px;
				break;
			default:
				num = 0f;
				num2 = px;
				break;
			}
			float num3 = px - thicknessPx;
			Color[] array = (Color[])(object)new Color[px * px];
			for (int i = 0; i < px; i++)
			{
				for (int j = 0; j < px; j++)
				{
					float num4 = (float)j + 0.5f - num;
					float num5 = (float)i + 0.5f - num2;
					float num6 = Mathf.Sqrt(num4 * num4 + num5 * num5);
					float num7 = Mathf.Clamp01((float)px - num6 + 0.5f);
					float num8 = Mathf.Clamp01(num6 - num3 + 0.5f);
					Color val2 = fill;
					val2.a = fill.a * num7 * num8;
					array[i * px + j] = val2;
				}
			}
			val.SetPixels(array);
			val.Apply();
			return val;
		}

		private Texture2D[] GetRingCorners(RoundVariant variant, int thicknessPx)
		{
			if (variant.RingThicknessPx == thicknessPx && variant.Rings[0] != null)
			{
				return variant.Rings;
			}
			for (int i = 0; i < 4; i++)
			{
				if (variant.Rings[i] != null)
				{
					uiTextures.Remove(variant.Rings[i]);
					UnityEngine.Object.Destroy(variant.Rings[i]);
					variant.Rings[i] = null;
				}
				Texture2D val = MakeRingCornerTexture(variant.RadiusPx, variant.Fill, (Corner)i, thicknessPx);
				variant.Rings[i] = val;
				uiTextures.Add(val);
			}
			variant.RingThicknessPx = thicknessPx;
			return variant.Rings;
		}

		private static long VariantKey(int radiusPx, Color fill)
		{
			return (17L * 31L + radiusPx) * 31 + fill.GetHashCode();
		}

		private RoundVariant GetRoundVariant(int radiusPx, Color fill)
		{
			long key = VariantKey(radiusPx, fill);
			if (roundVariantIndex.TryGetValue(key, out var value) && value.RadiusPx == radiusPx && value.Fill == fill && value.Corners[0] != null)
			{
				return value;
			}
			if (roundVariants.Count >= 48)
			{
				RetireTextures();
			}
			value = new RoundVariant
			{
				RadiusPx = radiusPx,
				Fill = fill,
				Corners = (Texture2D[])(object)new Texture2D[4],
				Rings = (Texture2D[])(object)new Texture2D[4],
				RingThicknessPx = 0
			};
			for (int i = 0; i < 4; i++)
			{
				Texture2D val = MakeCornerTexture(radiusPx, fill, (Corner)i);
				value.Corners[i] = val;
				uiTextures.Add(val);
			}
			roundVariants.Add(value);
			roundVariantIndex[key] = value;
			return value;
		}

		public void DrawRoundBox(Rect rect, float radiusUnits, Color fill)
		{
			if (!(rect.width < 1f) && !(rect.height < 1f))
			{
				float num = MeasureDevicePixelsPerPoint();
				rect = SnapToDevicePixelRect(rect, num);
				float num2 = Mathf.Min(rect.width, rect.height) * 0.5f;
				float num3 = Mathf.Min(radiusUnits, num2);
				num3 = Mathf.Clamp(Mathf.Floor(num3 * num) / num, Mathf.Min(2f, num2), num2);
				if (num3 < 2f)
				{
					GUI.DrawTexture(rect, (Texture)(object)Tex(fill));
					return;
				}
				int radiusPx = Mathf.Clamp(Mathf.RoundToInt(num3 * assetsScale), 6, 96);
				RoundVariant roundVariant = GetRoundVariant(radiusPx, fill);
				GUI.DrawTexture(new Rect(rect.x, rect.y, num3, num3), (Texture)(object)roundVariant.Corners[0]);
				GUI.DrawTexture(new Rect(rect.xMax - num3, rect.y, num3, num3), (Texture)(object)roundVariant.Corners[1]);
				GUI.DrawTexture(new Rect(rect.x, rect.yMax - num3, num3, num3), (Texture)(object)roundVariant.Corners[2]);
				GUI.DrawTexture(new Rect(rect.xMax - num3, rect.yMax - num3, num3, num3), (Texture)(object)roundVariant.Corners[3]);
				GUI.DrawTexture(new Rect(rect.x + num3, rect.y, rect.width - 2f * num3, rect.height), (Texture)(object)Tex(fill));
				GUI.DrawTexture(new Rect(rect.x, rect.y + num3, num3, rect.height - 2f * num3), (Texture)(object)Tex(fill));
				GUI.DrawTexture(new Rect(rect.xMax - num3, rect.y + num3, num3, rect.height - 2f * num3), (Texture)(object)Tex(fill));
			}
		}

		public void DrawRoundOutline(Rect rect, float radiusUnits, Color line, float thicknessUnits)
		{
			if (!(rect.width < 2f) && !(rect.height < 2f))
			{
				float num = MeasureDevicePixelsPerPoint();
				rect = SnapToDevicePixelRect(rect, num);
				float num2 = Mathf.Min(rect.width, rect.height) * 0.5f;
				float num3 = Mathf.Min(thicknessUnits, num2);
				num3 = Mathf.Max(1f / num, Mathf.Floor(num3 * num) / num);
				float num4 = Mathf.Min(radiusUnits, num2);
				num4 = Mathf.Clamp(Mathf.Floor(num4 * num) / num, Mathf.Min(2f, num2), num2);
				if (num4 < 2f)
				{
					GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, num3), (Texture)(object)Tex(line));
					GUI.DrawTexture(new Rect(rect.x, rect.yMax - num3, rect.width, num3), (Texture)(object)Tex(line));
					GUI.DrawTexture(new Rect(rect.x, rect.y + num3, num3, rect.height - 2f * num3), (Texture)(object)Tex(line));
					GUI.DrawTexture(new Rect(rect.xMax - num3, rect.y + num3, num3, rect.height - 2f * num3), (Texture)(object)Tex(line));
					return;
				}
				int num5 = Mathf.Clamp(Mathf.RoundToInt(num4 * assetsScale), 6, 96);
				int thicknessPx = Mathf.Clamp(Mathf.RoundToInt(num3 * assetsScale), 1, num5);
				Texture2D[] ringCorners = GetRingCorners(GetRoundVariant(num5, line), thicknessPx);
				GUI.DrawTexture(new Rect(rect.x, rect.y, num4, num4), (Texture)(object)ringCorners[0]);
				GUI.DrawTexture(new Rect(rect.xMax - num4, rect.y, num4, num4), (Texture)(object)ringCorners[1]);
				GUI.DrawTexture(new Rect(rect.x, rect.yMax - num4, num4, num4), (Texture)(object)ringCorners[2]);
				GUI.DrawTexture(new Rect(rect.xMax - num4, rect.yMax - num4, num4, num4), (Texture)(object)ringCorners[3]);
				GUI.DrawTexture(new Rect(rect.x + num4, rect.y, rect.width - 2f * num4, num3), (Texture)(object)Tex(line));
				GUI.DrawTexture(new Rect(rect.x + num4, rect.yMax - num3, rect.width - 2f * num4, num3), (Texture)(object)Tex(line));
				GUI.DrawTexture(new Rect(rect.x, rect.y + num4, num3, rect.height - 2f * num4), (Texture)(object)Tex(line));
				GUI.DrawTexture(new Rect(rect.xMax - num3, rect.y + num4, num3, rect.height - 2f * num4), (Texture)(object)Tex(line));
			}
		}

		public static GUIStyle MakeStyle(int fontSize, FontStyle fontStyle, TextAnchor anchor, Color textColor, Texture2D background, RectOffset padding)
		{
			GUIStyle val = new GUIStyle();
			val.fontSize = fontSize;
			val.fontStyle = fontStyle;
			val.alignment = anchor;
			val.normal.textColor = textColor;
			if (background != null)
			{
				val.normal.background = background;
			}
			if (padding != null)
			{
				val.padding = padding;
			}
			return val;
		}

		public static GUIStyle MakeDropdownItemStyle(Color textColor, bool selected)
		{
			GUIStyle obj = MakeStyle(13, (FontStyle)(selected ? 1 : 0), (TextAnchor)3, textColor, null, new RectOffset(10, 8, 4, 4));
			obj.wordWrap = false;
			obj.clipping = (TextClipping)1;
			return obj;
		}

		public static float Damp(float current, float target, float speed, float deltaTime)
		{
			float num = 1f - Mathf.Exp((0f - speed) * Mathf.Max(0f, deltaTime));
			float num2 = current + (target - current) * num;
			if (!(Mathf.Abs(target - num2) < 0.0015f))
			{
				return num2;
			}
			return target;
		}

		public static void WithAlpha(float alpha, Action draw)
		{
			if (alpha <= 0.003f)
			{
				return;
			}
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, color.a * Mathf.Clamp01(alpha));
			try
			{
				draw();
			}
			finally
			{
				GUI.color = color;
			}
		}

		static UiSkin()
		{
			ColBg = new Color32((byte)16, (byte)18, (byte)22, byte.MaxValue);
			ColSurface = new Color32((byte)25, (byte)29, (byte)36, byte.MaxValue);
			ColRaised = new Color32((byte)32, (byte)37, (byte)45, byte.MaxValue);
			ColInput = new Color32((byte)20, (byte)24, (byte)30, byte.MaxValue);
			ColBorder = new Color32((byte)42, (byte)48, (byte)58, byte.MaxValue);
			ColLine = new Color32((byte)37, (byte)43, (byte)52, byte.MaxValue);
			ColText = new Color32((byte)237, (byte)240, (byte)244, byte.MaxValue);
			ColMuted = new Color32((byte)152, (byte)162, (byte)178, byte.MaxValue);
			ColFaint = new Color32((byte)115, (byte)127, (byte)144, byte.MaxValue);
			ColAccent = new Color32((byte)214, (byte)183, (byte)121, byte.MaxValue);
			ColAccentSoft = new Color(0.839f, 0.718f, 0.475f, 0.14f);
			ColAccentLine = new Color(0.839f, 0.718f, 0.475f, 0.32f);
			ColGreen = new Color32((byte)126, (byte)200, (byte)164, byte.MaxValue);
			ColRed = new Color32((byte)238, (byte)153, (byte)158, byte.MaxValue);
			ColHoverRaised = new Color32((byte)38, (byte)44, (byte)53, byte.MaxValue);
			ColHoverBorder = new Color32((byte)58, (byte)66, (byte)79, byte.MaxValue);
			ColOverlay = new Color(0f, 0f, 0f, 0.55f);
			ColGrid = new Color(1f, 1f, 1f, 0.055f);
			ColShadowOuter = new Color(0f, 0f, 0f, 0.14f);
			ColShadowInner = new Color(0f, 0f, 0f, 0.2f);
			ColPanel = new Color(0.063f, 0.071f, 0.086f, 0.45f);
			ColPanelShadow = new Color(0f, 0f, 0f, 0.22f);
			ColTrackOff = new Color32((byte)44, (byte)50, (byte)60, byte.MaxValue);
			ColKnob = new Color32((byte)244, (byte)246, (byte)250, byte.MaxValue);
			ColKnobOn = new Color32((byte)34, (byte)27, (byte)16, byte.MaxValue);
			ColKnobShadow = new Color(0f, 0f, 0f, 0.28f);
			ColFocus = new Color(0.839f, 0.718f, 0.475f, 0.55f);
			ColPressed = new Color(0f, 0f, 0f, 0.18f);
		}
	}
}
