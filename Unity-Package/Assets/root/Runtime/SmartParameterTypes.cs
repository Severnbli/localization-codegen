using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Metadata;

namespace Severnbli.LocalizationCodegen
{
    /// <summary>Parameter type a generated smart string method uses.</summary>
    public enum SmartParameterType
    {
        Object = 0,
        String = 1,
        Int = 2,
        Long = 3,
        Float = 4,
        Double = 5,
        Decimal = 6,
        Bool = 7,
        Char = 8,
        DateTime = 9,
        List = 10
    }

    /// <summary>
    /// Shared entry metadata that sets the parameter types of a generated smart string method when
    /// <b>Smart Parameter Types</b> is <c>Mapping</c>. Parameters without a type here are inferred.
    /// </summary>
    /// <remarks>
    /// Only the code generator reads this metadata. It lives in the runtime assembly because table assets
    /// serialize it, and a player build must be able to load the type.
    /// </remarks>
    [Metadata(AllowedTypes = MetadataType.SharedStringTableEntry, AllowMultiple = false,
        MenuItem = "Localization Codegen/Smart Parameter Types")]
    [Serializable]
    public sealed class SmartParameterTypes : IMetadata
    {
        /// <summary>The type of one smart string variable.</summary>
        [Serializable]
        public sealed class Parameter
        {
            [SerializeField]
            [Tooltip("Variable name as written in the smart string, e.g. count, or an index such as 0 for {0}.")]
            string m_Name = "";

            [SerializeField]
            SmartParameterType m_Type = SmartParameterType.Object;

            public Parameter()
            {
            }

            public Parameter(string name, SmartParameterType type)
            {
                m_Name = name ?? "";
                m_Type = type;
            }

            public string Name
            {
                get => m_Name ?? "";
                set => m_Name = value ?? "";
            }

            public SmartParameterType Type
            {
                get => m_Type;
                set => m_Type = value;
            }
        }

        [SerializeField]
        List<Parameter> m_Parameters = new List<Parameter>();

        public List<Parameter> Parameters => m_Parameters ?? (m_Parameters = new List<Parameter>());
    }
}
