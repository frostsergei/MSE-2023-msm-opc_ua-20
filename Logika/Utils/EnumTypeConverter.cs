using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Logika.Utils {
    /// <summary>
    /// TypeConverter для Enum, преобразовывающий Enum к строке с
    /// учетом атрибута Description
    /// </summary>
    
    public class EnumTypeConverter : EnumConverter {
        private Type _enumType;
        /// <summary>Инициализирует экземпляр</summary>
        /// <param name="type">тип Enum</param>
        public EnumTypeConverter(Type type)
            : base(type)
        {
            _enumType = type;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                _enumType = Nullable.GetUnderlyingType(type);
            }
        }

        public override bool CanConvertTo(ITypeDescriptorContext context,
          Type destType)
        {            
            return destType == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context,
          CultureInfo culture,
          object value, Type destType)
        {
            if (value == null)
                return "";
            FieldInfo fi = _enumType.GetField(Enum.GetName(_enumType, value));
            DescriptionAttribute dna =
              (DescriptionAttribute)Attribute.GetCustomAttribute(
                fi, typeof(DescriptionAttribute));

            if (dna != null)
                return dna.Description;
            else
                return value.ToString();
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context,
          Type srcType)
        {
            return srcType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context,
          CultureInfo culture,
          object value)
        {
            foreach (FieldInfo fi in _enumType.GetFields()) {
                DescriptionAttribute dna =
                  (DescriptionAttribute)Attribute.GetCustomAttribute(
                    fi, typeof(DescriptionAttribute));

                if ((dna != null) && ((string)value == dna.Description))
                    return Enum.Parse(_enumType, fi.Name);
            }

            return Enum.Parse(_enumType, (string)value);
        }

    }

}
