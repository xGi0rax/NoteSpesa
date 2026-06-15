namespace Ergon.Services
{
    public class Utils
    {
        // Verifica se una data rappresenta un giorno festivo tenendo conto anche della Pasqua
        // Il sabato non viene considerato come un giorno festivo
        public static bool IsHoliday(DateTime data)
        {
            if (data.DayOfWeek == DayOfWeek.Sunday) return true;

            if(data.Day == 1 && (data.Month == 1 || data.Month == 5) ||
               data.Day == 2 && data.Month == 6 ||
               data.Day == 8 && data.Month == 12 ||
               data.Day == 15 && data.Month == 8 ||
               data.Day == 25 && (data.Month == 4 || data.Month == 12) ||
               data.Day == 26 && data.Month == 12) return true;

            DateTime easter = GetEasterDay(data.Year);
            if (data.Date == easter.Date || data.Date == easter.Date.AddDays(1)) return true;

            return false;
        }
        // Calcola la data esatta della Pasqua in base all'anno
        public static DateTime GetEasterDay(int year)
        {
            int g = year % 19;
            int c = year / 100;
            int h = (c - (c / 4) - ((8 * c + 13) / 25) + (19 * g) + 15) % 30;
            int i = h - (h / 28 * (1 - (h / 28) * (29 / (h + 1)) * ((21 - g) / 11)));
            int j = (year + (year / 4) + i + 2 - c + (c / 4)) % 7;
            int l = i - j;
            int month = 3 + ((l + 40) / 44);
            int day = l + 28 - (31 * (month / 4));

            return new DateTime(year, month, day);
        }
    }
}
