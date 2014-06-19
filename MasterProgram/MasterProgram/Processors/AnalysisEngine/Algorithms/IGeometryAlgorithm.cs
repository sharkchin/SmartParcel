using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dmp.Neptune.Collections;

namespace DMP.MasterProgram.Processors.AnalysisEngine.Algorithms
{
    interface IGeometryAlgorithm
    {
        //output should be object
        //in case of intesects or intersection,union,difference(It should be List of Abstract records)
        //in case of intersection,union abstract record is created witj two parameters Type and AEGeometry. 
        object ProcessRecord(AbstractRecord record,bool isSubByTask);
        void InitializeImpactors(List<AbstractRecord> impactors);
        void InitializeParameters(Dictionary<String, String> parameters);
    }
}
