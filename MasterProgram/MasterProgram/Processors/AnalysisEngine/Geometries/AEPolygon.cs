/**
 * 
 * 
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Geometries
{
    public class AEPolygon : AEGeometry
    {
        private double[] xCordinates;
        private double[] yCordinates;
        private int numOfCordinates;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xCordinates">Array of X Cordinates</param>
        /// <param name="yCordinates">Array of Y Cordinates</param>
        public AEPolygon(double[] xCordinates, double[] yCordinates)
        {
            if (xCordinates.Length != yCordinates.Length)
            {
                //throe Exception
            }


            this.xCordinates = xCordinates;
            this.yCordinates = yCordinates;
            this.numOfCordinates = xCordinates.Length;

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="points">Array of Points</param>
        public AEPolygon(AEPoint[] points)
        {
            double[] xCords = new double[points.Length];
            double[] yCords = new double[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                xCords[i] = points[i].XCordinate;
                yCords[i] = points[i].YCordinate;

            }

            this.xCordinates = xCords;
            this.yCordinates = yCords;
        }

        /// <summary>
        /// Array of X-Cordinates of Polygon
        /// </summary>
        public double[] XCordinates
        {
            get
            {
                return this.xCordinates;
            }
        }


        /// <summary>
        /// Array of Y-Cordinates of Polygon
        /// </summary>
        public double[] YCordinates
        {
            get
            {
                return this.yCordinates;
            }
        }

        /// <summary>
        /// Number of Points of Polygon
        /// </summary>
        public int NumberOfPoints
        {
            get 
            {
                return this.numOfCordinates;
            }
            set
            {
                this.numOfCordinates = value;
            }
        }
    }
}
