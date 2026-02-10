using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public static class DeepCloneHelper
    {
        /// <summary>
        /// Deep clone using Newtonsoft.Json serialization + deserialization.
        /// </summary>
        public static T DeepClone<T>(this T obj, JsonSerializerSettings settings = null)
        {
            if (object.ReferenceEquals(obj, null))
                return default(T);

            if (settings == null)
                settings = DefaultSettings;

            var json = JsonConvert.SerializeObject(obj, settings);
            return JsonConvert.DeserializeObject<T>(json, settings);
        }

        private static readonly JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None
        };
    }
}
