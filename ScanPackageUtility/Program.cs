using NuGet;
using System;
using System.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static ScanPackageUtility.SearchResults;

namespace ScanPackageUtility
{
    /// <summary>
    /// Escanea recursivamente las soluciones y proyectos del directorio indicado para generar un reporte detallado de sus dependencias
    /// 
    /// Basado en el codigo compartido por Puneet Ghanshani en su blog: https://blogs.msdn.microsoft.com/modernarchitecturedevops/2017/01/20/nuget-packages-inventory-across-multiple-solutions/
    /// una version mas completa que la documentada originalmente por Matt Ward en: http://stackoverflow.com/questions/33035704/how-to-read-the-list-of-nuget-packages-in-packages-config-programatically
    /// 
    /// NOTA: los paths y el nombe del archivo de reporte se deben configurar en el App.config
    /// </summary>
    class Program
    {
        private const string SEPARADOR = "_";

        //Se cargan desde el App.config los valores configurados
        //Ruta desde donde se inicia el escaneo";
        static private readonly string pathScanBegin = ConfigurationManager.AppSettings["pathScanBegin"];
        //Ruta donde se desea obtener el reporte
        static private readonly string pathReporting = ConfigurationManager.AppSettings["pathReporting"];
        //Nombre del archivo de reporte
        static private readonly string reportName = ConfigurationManager.AppSettings["reportName"];

        static void Main(string[] args)
        {

            try
            {
                //Se buscan todos los archivos 'package.config' localizados en todos los directorios ubicados bajo la ruta de inicio del escaneo
                Console.WriteLine("Escaneando las soluciones a partir de: [" + @pathScanBegin + "]");
                string[] files = Directory.GetFiles(@pathScanBegin, "packages.config", SearchOption.AllDirectories);

                //Formato del registro
                const string format = "{0},{1},{2}";
                StringBuilder builder = new StringBuilder();
                List<SearchResults> results = new List<SearchResults>();

                //Se extrae la informacion de cada uno de los archivos 'package.config' localizados durante el escaneo
                foreach (var fileName in files)
                {
                    var file = new PackageReferenceFile(fileName);
                    foreach (PackageReference packageReference in file.GetPackageReferences())
                    {
                        SearchResults currentResult = results.FirstOrDefault(x => x.PackageId == packageReference.Id);
                        if (currentResult == null)
                        {
                            currentResult = new SearchResults
                            {
                                PackageId = packageReference.Id
                            };
                            results.Add(currentResult);
                        }

                        SearchVersion currentVersion = currentResult.Versions.FirstOrDefault(x => x.PackageVersion == packageReference.Version.ToString());
                        if (currentVersion == null)
                        {
                            currentVersion = new SearchResults.SearchVersion
                            {
                                PackageVersion = packageReference.Version.ToString()
                            };
                            currentResult.Versions.Add(currentVersion);
                        }

                        currentVersion.Path.Add(fileName);
                    }
                }

                //Se construye el registro para cada resultado encontrado
                results.ForEach(result =>
                {
                    if (result.Versions.Count > 1) // multiple versions of same package
                    {
                        result.Versions.ForEach(version =>
                        {
                            version.Path.ForEach(packagePath =>
                            {
                                //Aqui se define la estructura del registro que debe corresponder al formato definido inicialmente
                                builder.AppendFormat(format, result.PackageId, packagePath, version.PackageVersion);
                                builder.AppendLine();
                            });
                        });
                    }
                });

                //Prepara el reporte
                string nameReport = prepareReport();

                //Se escribe el inventario generado en el archivo de reporte
                File.WriteAllText(Path.Combine(@pathReporting, nameReport), builder.ToString());
                Console.WriteLine("El inventario ha sido generado.");
                Console.WriteLine("El archivo [" + nameReport + "] ha sido colocado en la ruta: [" + @pathReporting + "]");
                Console.WriteLine("\nPulse una tecla para salir...");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine("El inventario no pudo ser generado.");
                Console.WriteLine("\nOcurrio un error: [" + e.Message + "]");
                Console.ReadKey();
                //throw;
            }

        }

        /// <summary>
        /// Si no existe la ruta del reporte la crea y en caso de existir el reporte lo respalda
        /// </summary>
        /// <returns></returns>
        private static string prepareReport()
        {
            //Si el directorio no existe, es creado
            if (!Directory.Exists(@pathReporting))
            {
                Directory.CreateDirectory(@pathReporting);
            }

            //Se prepara el nombre del archivo
            string filename = buildFilename();

            //Si el archivo existe en el directorio se respalda
            if (File.Exists(Path.Combine(@pathReporting, filename)))
            {
                int count = 0;
                string backup = "";
                do
                {
                    backup = Path.GetFileNameWithoutExtension(filename) + SEPARADOR + count + (".bak");
                    count++;
                } while (File.Exists(Path.Combine(@pathReporting, backup)));
                File.Move(Path.Combine(@pathReporting, filename), Path.Combine(@pathReporting, backup));
            }

            return filename;

        }

        /// <summary>
        /// Construye el nombre del archivo de reporte a partir del valor configurado y la fecha
        /// </summary>
        /// <returns></returns>
        static private string buildFilename()
        {
            DateTime fecha = DateTime.Now; // Instancio un objeto DateTime
            StringBuilder builderFilename = new StringBuilder();
            builderFilename.Append(reportName);
            builderFilename.Append(SEPARADOR).Append(fecha.Year.ToString())
                .Append(SEPARADOR).Append(fecha.Month.ToString())
                .Append(SEPARADOR).Append(fecha.Day.ToString());
            builderFilename.Append(".csv");

            return builderFilename.ToString();
        }
    }

    /// <summary>
    /// InnerClass para los resultados del scan
    /// </summary>
    public class SearchResults
    {
        public string PackageId { get; set; }
        public List<SearchVersion> Versions { get; set; }
        public SearchResults()
        {
            Versions = new List<SearchResults.SearchVersion>();
        }

        public class SearchVersion
        {
            public string PackageVersion { get; set; }
            public List<string> Path { get; set; }
            public SearchVersion()
            {
                Path = new List<string>();
            }
        }
    }

}
