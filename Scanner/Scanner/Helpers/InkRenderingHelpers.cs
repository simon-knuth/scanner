using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Input.Inking;

namespace Scanner.Helpers;

/// <summary>
/// Renders Windows Ink strokes, and moves them between coordinate spaces.
/// </summary>
/// <remarks>
/// Win2D's WinUI 3 build doesn't offer the ink renderer that UWP had, so strokes are rasterized from their
/// rendering segments. Pen tips other than <see cref="PenTipShape.Circle"/> are approximated by their largest
/// dimension and <see cref="InkDrawingAttributes.PenTipTransform"/> is not applied; neither is used by the pens
/// that the InkToolbar offers.
/// </remarks>
public static class InkRenderingHelpers
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Constants
    /// <summary>
    /// The pressure at which a stroke is drawn at exactly its <see cref="InkDrawingAttributes.Size"/>. Input devices
    /// without pressure support (like a mouse) report this value for every point.
    /// </summary>
    private const float nominalPressure = 0.5f;

    /// <summary>
    /// Bounds for the factor by which pressure may scale a stroke's width, so that outliers can't turn a stroke
    /// into a hairline or a blob.
    /// </summary>
    private const float minPressureFactor = 0.25f;
    private const float maxPressureFactor = 2.0f;

    /// <summary>
    /// Pressure differences below this are treated as constant, which lets a stroke be drawn in a single pass.
    /// </summary>
    private const float pressureTolerance = 0.01f;
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Converts strokes collected on an ink canvas into the page's own pixel space, so that they can be stored on
    /// the page without depending on how the canvas happened to be sized and positioned over it.
    /// </summary>
    /// <param name="strokes">The strokes as collected, in the ink canvas' coordinate space.</param>
    /// <param name="pageArea">The page's bounds within that same coordinate space.</param>
    /// <param name="pageSize">The page's size in pixels.</param>
    public static List<InkStroke> ConvertToPageSpace(IReadOnlyList<InkStroke> strokes, Rect pageArea, Size pageSize)
    {
        float scale = 1.0f;
        if (pageArea.Width > 0 && pageSize.Width > 0)
            scale = (float)(pageSize.Width / pageArea.Width);

        // the page doesn't necessarily start at the ink canvas' origin, so shift before scaling
        Matrix3x2 inkToPage = Matrix3x2.CreateTranslation((float)-pageArea.X, (float)-pageArea.Y)
            * Matrix3x2.CreateScale(scale);

        List<InkStroke> result = new(strokes.Count);
        foreach (InkStroke stroke in strokes)
        {
            result.Add(TransformStroke(stroke, inkToPage, scale));
        }
        return result;
    }

    /// <summary>
    /// Converts strokes stored on a page back into an ink canvas' coordinate space, so that they can be handed to
    /// an <c>InkCanvas</c> for further editing. The inverse of <see cref="ConvertToPageSpace"/>.
    /// </summary>
    /// <param name="strokes">The strokes, in the page's pixel space.</param>
    /// <param name="pageArea">The page's bounds within the ink canvas' coordinate space.</param>
    /// <param name="pageSize">The page's size in pixels.</param>
    public static List<InkStroke> ConvertFromPageSpace(IReadOnlyList<InkStroke> strokes, Rect pageArea, Size pageSize)
    {
        float scale = 1.0f;
        if (pageSize.Width > 0 && pageArea.Width > 0)
            scale = (float)(pageArea.Width / pageSize.Width);

        Matrix3x2 pageToInk = Matrix3x2.CreateScale(scale)
            * Matrix3x2.CreateTranslation((float)pageArea.X, (float)pageArea.Y);

        List<InkStroke> result = new(strokes.Count);
        foreach (InkStroke stroke in strokes)
        {
            result.Add(TransformStroke(stroke, pageToInk, scale));
        }
        return result;
    }

    /// <summary>
    /// Moves strokes along with the page they sit on, for example when it is cropped or rotated.
    /// </summary>
    /// <remarks>
    /// Only intended for transforms without a scale component, which is what crop and rotate are: stroke widths are
    /// carried over unchanged, so a scaling transform would leave them at their old size.
    /// </remarks>
    public static List<InkStroke> TransformStrokes(IReadOnlyList<InkStroke> strokes, Matrix3x2 transform)
    {
        List<InkStroke> result = new(strokes.Count);
        foreach (InkStroke stroke in strokes)
        {
            result.Add(TransformStroke(stroke, transform, 1.0f));
        }
        return result;
    }

    /// <summary>
    /// Draws <paramref name="background"/> into <paramref name="session"/> with <paramref name="strokes"/> on top.
    /// </summary>
    /// <remarks>
    /// The strokes are expected to be in the same coordinate space as <paramref name="background"/>, which is what
    /// <see cref="ConvertToPageSpace"/> produces.
    /// </remarks>
    /// <param name="session">The session to draw into.</param>
    /// <param name="resourceCreator">Used to create the intermediate layers that highlighter ink needs.</param>
    /// <param name="background">The page to draw the strokes on top of.</param>
    /// <param name="strokes">The strokes to draw.</param>
    public static void DrawImageWithInk(CanvasDrawingSession session, ICanvasResourceCreator resourceCreator,
        ICanvasImage background, IReadOnlyList<InkStroke> strokes)
    {
        if (strokes.Count == 0)
        {
            session.DrawImage(background);
            return;
        }

        // highlighter ink tints the page instead of covering it, so it has to be blended in separately
        List<InkStroke> penStrokes = [];
        List<InkStroke> highlighterStrokes = [];
        foreach (InkStroke stroke in strokes)
        {
            if (stroke.DrawingAttributes.DrawAsHighlighter)
                highlighterStrokes.Add(stroke);
            else
                penStrokes.Add(stroke);
        }

        if (highlighterStrokes.Count == 0)
        {
            // nothing to blend, so the strokes can go straight into the session
            session.DrawImage(background);
            DrawStrokes(session, penStrokes);
            return;
        }

        using CanvasCommandList penLayer = new(resourceCreator);
        using (CanvasDrawingSession penSession = penLayer.CreateDrawingSession())
        {
            penSession.DrawImage(background);
            DrawStrokes(penSession, penStrokes);
        }

        using CanvasCommandList highlighterLayer = new(resourceCreator);
        using (CanvasDrawingSession highlighterSession = highlighterLayer.CreateDrawingSession())
        {
            DrawStrokes(highlighterSession, highlighterStrokes);
        }

        using BlendEffect highlighterBlend = new()
        {
            Background = penLayer,
            Foreground = highlighterLayer,
            Mode = BlendEffectMode.Multiply
        };
        session.DrawImage(highlighterBlend);
    }

    private static InkStroke TransformStroke(InkStroke stroke, Matrix3x2 transform, float sizeScale)
    {
        InkStroke result = stroke.Clone();
        result.PointTransform = result.PointTransform * transform;

        if (sizeScale != 1.0f)
        {
            InkDrawingAttributes attributes = result.DrawingAttributes;
            attributes.Size = new Size(attributes.Size.Width * sizeScale, attributes.Size.Height * sizeScale);
            result.DrawingAttributes = attributes;
        }

        return result;
    }

    private static void DrawStrokes(CanvasDrawingSession session, List<InkStroke> strokes)
    {
        foreach (InkStroke stroke in strokes)
        {
            IReadOnlyList<InkStrokeRenderingSegment> segments = stroke.GetRenderingSegments();
            if (segments.Count < 2)
                continue;

            InkDrawingAttributes attributes = stroke.DrawingAttributes;
            float width = (float)Math.Max(attributes.Size.Width, attributes.Size.Height);
            if (width <= 0)
                continue;

            // the segments already carry the stroke's PointTransform, so it must not be applied again here
            using CanvasStrokeStyle strokeStyle = CreateStrokeStyle(attributes.PenTip);

            if (attributes.IgnorePressure)
                DrawStrokeWithConstantWidth(session, segments, attributes.Color, width, strokeStyle);
            else if (TryGetUniformPressure(segments, out float pressure))
                DrawStrokeWithConstantWidth(session, segments, attributes.Color, width * GetPressureFactor(pressure), strokeStyle);
            else
                DrawStrokeWithPressure(session, segments, attributes.Color, width, strokeStyle);
        }
    }

    private static void DrawStrokeWithConstantWidth(CanvasDrawingSession session, IReadOnlyList<InkStrokeRenderingSegment> segments,
        Color color, float width, CanvasStrokeStyle strokeStyle)
    {
        using CanvasPathBuilder pathBuilder = new(session);
        pathBuilder.BeginFigure(ToVector2(segments[0].Position));
        for (int i = 1; i < segments.Count; i++)
        {
            InkStrokeRenderingSegment segment = segments[i];
            pathBuilder.AddCubicBezier(
                ToVector2(segment.BezierControlPoint1),
                ToVector2(segment.BezierControlPoint2),
                ToVector2(segment.Position));
        }
        pathBuilder.EndFigure(CanvasFigureLoop.Open);

        using CanvasGeometry geometry = CanvasGeometry.CreatePath(pathBuilder);
        session.DrawGeometry(geometry, color, width, strokeStyle);
    }

    /// <summary>
    /// Draws a stroke one segment at a time, so that every segment can carry the width its pressure calls for.
    /// </summary>
    private static void DrawStrokeWithPressure(CanvasDrawingSession session, IReadOnlyList<InkStrokeRenderingSegment> segments,
        Color color, float width, CanvasStrokeStyle strokeStyle)
    {
        Vector2 start = ToVector2(segments[0].Position);
        for (int i = 1; i < segments.Count; i++)
        {
            InkStrokeRenderingSegment segment = segments[i];
            Vector2 end = ToVector2(segment.Position);

            using (CanvasPathBuilder pathBuilder = new(session))
            {
                pathBuilder.BeginFigure(start);
                pathBuilder.AddCubicBezier(
                    ToVector2(segment.BezierControlPoint1),
                    ToVector2(segment.BezierControlPoint2),
                    end);
                pathBuilder.EndFigure(CanvasFigureLoop.Open);

                using CanvasGeometry geometry = CanvasGeometry.CreatePath(pathBuilder);
                session.DrawGeometry(geometry, color, width * GetPressureFactor(segment.Pressure), strokeStyle);
            }

            start = end;
        }
    }

    private static bool TryGetUniformPressure(IReadOnlyList<InkStrokeRenderingSegment> segments, out float pressure)
    {
        pressure = segments[0].Pressure;
        for (int i = 1; i < segments.Count; i++)
        {
            if (Math.Abs(segments[i].Pressure - pressure) > pressureTolerance)
                return false;
        }
        return true;
    }

    private static float GetPressureFactor(float pressure)
    {
        return Math.Clamp(pressure / nominalPressure, minPressureFactor, maxPressureFactor);
    }

    private static Vector2 ToVector2(Point point)
    {
        return new Vector2((float)point.X, (float)point.Y);
    }

    private static CanvasStrokeStyle CreateStrokeStyle(PenTipShape penTip)
    {
        if (penTip == PenTipShape.Rectangle)
        {
            return new CanvasStrokeStyle
            {
                StartCap = CanvasCapStyle.Square,
                EndCap = CanvasCapStyle.Square,
                LineJoin = CanvasLineJoin.Miter
            };
        }

        return new CanvasStrokeStyle
        {
            StartCap = CanvasCapStyle.Round,
            EndCap = CanvasCapStyle.Round,
            LineJoin = CanvasLineJoin.Round
        };
    }
}
