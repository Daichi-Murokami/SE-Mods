using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Utils;

using Churrosaur.Cables;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using System;

namespace Churrosaur.Bezier
{
    public class BezierDrawer
    {
        #region static methods
        // Static vector/draw methods below har

        public static Vector3D mid(Vector3D p1, Vector3D p2)
        {
            var m = p2 - p1;
            m *= 0.5;
            return m + p1;
        }

        public static void drawLine(Vector3D p1, Vector3D p2, Color color)
        {
            var cv = color.ToVector4();
            MySimpleObjectDraw.DrawLine(p1, p2, MyStringId.GetOrCompute("Square"), ref cv, .03f, BlendTypeEnum.SDR);
        }

        public static void drawCurve(Vector3D p1, Vector3D p2, Vector3D handle, int iterations, Color color)
        {
            var lMid = mid(p1, handle);
            var rMid = mid(p2, handle);
            var mMid = mid(lMid, rMid);

            if (iterations <= 0)
            {
                drawLine(p1, mMid, color);
                drawLine(mMid, p2, color);
                return;
            }

            drawCurve(p1, mMid, lMid, iterations - 1, color);
            drawCurve(mMid, p2, rMid, iterations - 1, color);

        }
        #endregion

        // Instantiated line segment list stuff here
        // calculates to list then draws from list - saves overhead for static power lines etc.

        Color color = new Color(100, 100, 100, 255);

        public BezierDrawer() { }
        public BezierDrawer(Color c)
        {
            this.color = c;
        }

        public class LineSegment
        {
            Vector3D p1, p2;
            Color color;

            LineSegment() { }
            public LineSegment(Vector3D p1, Vector3D p2, Color color)
            {
                this.p1 = p1;
                this.p2 = p2;
                this.color = color;
            }

            public void draw()
            {
                drawLine(p1, p2, color);
            }
        }

        // list of segments, set when drawCurvePoints is called.
        private LinkedList<LineSegment> segList = new LinkedList<LineSegment>();

        public void drawCurveFromList()
        {
            foreach (LineSegment ln in segList)
            {
                ln.draw();
            }
        }

        // Wrapper for recursive draw to reset list et al.
        public void createCurvePoints(Vector3D p1, Vector3D p2, Vector3D handle, int iterations)
        {
            segList.Clear();
            calculatePoints(p1, p2, handle, iterations);
        }

        private void calculatePoints(Vector3D p1, Vector3D p2, Vector3D handle, int iterations)
        {
            //MyAPIGateway.Utilities.ShowNotification("Bezier is calcing points");
            var lMid = mid(p1, handle);
            var rMid = mid(p2, handle);
            var mMid = mid(lMid, rMid);

            if (iterations <= 0)
            {
                segList.AddLast(new LineSegment(p1, mMid, color));
                segList.AddLast(new LineSegment(mMid, p2, color));
                return;
            }
            
            calculatePoints(p1, mMid, lMid, iterations - 1);
            calculatePoints(mMid, p2, rMid, iterations - 1);

        }
    }
}