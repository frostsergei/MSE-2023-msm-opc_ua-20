using System;

namespace Logika.Utils {
    /// <summary>
    /// Атрибут для поддержки динамически показываемых свойств
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class DynamicPropertyFilterAttribute : Attribute {
        string _propertyName;

        /// <summary>
        /// Название property, от которого будет зависить видимость
        /// </summary>
        public string PropertyName
        {
            get { return _propertyName; }
        }

        string _showOn;

        /// <summary>
        /// Значения свойства, от которого зависит видимость 
        /// (через запятую, если несколько), при котором свойство, к
        /// которому применен атрибут, будет видимо. 
        /// </summary>
        public string ShowOn
        {
            get { return _showOn; }
        }
               
        /// <summary>
        /// Конструктор  
        /// </summary>
        /// <param name="propName">Название свойства, от которого будет 
        /// зависеть видимость</param>
        /// <param name="value">Значения свойства (через запятую, если несколько), 
        /// при котором свойство, к которому применен атрибут, будет видимо.
        /// </param>
        public DynamicPropertyFilterAttribute()
        {
        }

        public DynamicPropertyFilterAttribute(string propertyName, string value)
        {
            _propertyName = propertyName;
            _showOn = value;
        }
    }
    public class DynamicPropertyFilter2Attribute : DynamicPropertyFilterAttribute
    {
        public DynamicPropertyFilter2Attribute(string propertyName, string value)
            :base(propertyName, value)
        {            
        }
    }
}
