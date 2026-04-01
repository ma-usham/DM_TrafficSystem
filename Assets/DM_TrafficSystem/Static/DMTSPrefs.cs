using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Centralizes editor drawing preferences so scene tools share one visual configuration.
    /// </summary>
    public static class DMTSPrefs
    {
        // Control points
        public static float ControlPointHandleSize = 1f;
        public static Color ControlPointColor = Color.white;
        public static Color DraggedControlPointColor = Color.yellow;

        // Spline curve
        public static Color CurveColor = new Color(1f, 0.85f, 0.1f, 1f);
        public static float CurveWidth = 2.5f;
        public static float CurveEditWidth = 3f;

        // Spline end marker
        public static Color ActiveEndColor = Color.green;

        // Insert-preview handle
        public static Color InsertPreviewColor = new Color(0f, 0.85f, 1f, 0.9f);

        // Screen-space interaction thresholds (pixels)
        public static float PointScreenRadius = 10f;
        public static float EndpointScreenRadius = 16f;
        public static float InsertScreenThreshold = 25f;

        // Waypoints
        public static float WaypointSizeMultiplier = 0.15f;
        public static Color WaypointColor = new Color(0.1f, 0.55f, 0.2f, 1f);
        public static Color WaypointLineColor = new Color(0.1f, 0.55f, 0.2f, 1f);

        // View Roads page curve preview
        public static Color ViewRoadCurveColor = Color.cyan;

        // Scene label font sizes
        public static int PointLabelFontSize = 10;
        public static int InsertLabelFontSize = 11;
    }
}
