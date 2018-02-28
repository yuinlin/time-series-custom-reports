using System;

namespace Reports
{
    public class DoubleValueFormatter
    {
        public static string FormatDoubleValue(double value, bool fix, int places, string missingStr)
        {
            if (double.IsNaN(value))
                return missingStr;

            if (fix)
            {
                double roundVal = Math.Round(value, places, MidpointRounding.ToEven);
                string formatStr = "F" + places.ToString();
                return roundVal.ToString(formatStr);
            }
            else
            {
                return FormatSigFigsNumber(value, places);
            }
        }

        public static string FormatSigFigsNumber(double value, int sigfigs)
        {
            string str = "";
            try
            {
                if (value != 0.0)
                {
                    bool negative = false;
                    if (value < 0.0)
                    {
                        negative = true;
                        value = -1.0 * value;
                    }
                    int exponent = (int)Math.Floor(Math.Log(value) / Math.Log(10));
                    int places = exponent - sigfigs + 1;
                    double divisor = Math.Pow(10.0, (double)(places));

                    double round = (double)Math.Round(value / divisor);

                    decimal result = (decimal)round * (decimal)divisor;

                    if (negative)
                    {
                        result = (decimal)-1.0 * result;
                    }

                    string zeros = "";
                    if (places < 0)
                    {
                        int nz = -1 * places;
                        for (int i = 0; i < nz; i++)
                        {
                            zeros += "0";
                        }
                    }
                    string theFormatStr = "{0:." + zeros + "}";

                    str = string.Format(theFormatStr, result);
                }
                else
                {
                    string theFormatStr = "{0}";
                    str = string.Format(theFormatStr, value);
                }
            }
            catch { }

            return str;
        }
    }
}
