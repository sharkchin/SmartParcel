using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMP.MasterProgram.ProcessorMetadata
{
    class JavascriptProcessorMetadata
    {
        private string expressionScript;
        private Dictionary<string, string> parameters;

        public JavascriptProcessorMetadata()
        {
            //this.parameters = new Dictionary<string, string>();

        }

        /// <summary>
        /// Expression script
        /// </summary>
        public string ExpressionScript
        {
            get
            {
                return this.expressionScript;
            }
            set
            {
                this.expressionScript = value; 
            }

        }

        /// <summary>
        /// parameters map with key as parameter name and value as parameter value
        /// </summary>
        public Dictionary<string, string> Parameters
        {
            get
            {
                return this.parameters;
            }

            set
            {
                 this.parameters = value;
            }
          
        }
        
    }
}
