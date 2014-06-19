using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Geometries
{
    public class AEPoint : AEGeometry
    {
        private double xCordinate;
        private double yCordinate;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xCordinate">X Cordinate</param>
        /// <param name="yCordinate">Y Cordinate</param>
        public AEPoint(double xCordinate, double yCordinate)
        {
            this.xCordinate = xCordinate;
            this.yCordinate = yCordinate;
        }


        /// <summary>
        /// X Cordinate of Point
        /// </summary>
        public double XCordinate {
            get { return xCordinate;}
            set {xCordinate = value ;}
        }


        /// <summary>
        /// Y Cordinate of point
        /// </summary>
        public double YCordinate
        {
            get { return yCordinate; }
            set { yCordinate = value; }
        }


    }
}
