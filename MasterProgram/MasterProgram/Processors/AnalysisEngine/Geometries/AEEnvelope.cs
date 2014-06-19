using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Geometries
{
    public class AEEnvelope
    {
        private double xMin;
        private double yMin;
        private double xMax;
        private double yMax;

        /// <summary>
        /// Min X cordinate of envelope.
        /// </summary>
        public double XMin 
        {
            get
            {
                return this.xMin;
            }
            set
            {
                this.xMin = value;
            }
        }

        /// <summary>
        /// Minimum Y cordinate of envelope
        /// </summary>
        public double YMin
        {
            get
            {
                return this.yMin;
            }
            set
            {
                this.yMin = value;
            }
        }

        /// <summary>
        /// Maximum X cordinate of envelope
        /// </summary>
        public double XMax
        {
            get
            {
                return this.xMax;
            }
            set
            {
                this.xMax = value;
            }
        }

        /// <summary>
        /// Maximum Y cordinate of envelope
        /// </summary>
        public double YMax
        {
            get
            {
                return this.yMax;
            }
            set
            {
                this.yMax = value;
            }
        }


    }
}
