using System.Collections.Generic;
using System.Globalization;

namespace OoLunar.CookieClicker.Entities.CommandFramework
{
    public interface ITranslationProvider
    {
        Dictionary<CultureInfo, string> Translate(string key);
    }
}
