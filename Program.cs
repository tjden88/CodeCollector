using System.Text;

namespace CsFolderBundler;

internal static class Program
{
    private static readonly HashSet<string> ExcludedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj"
    };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            var baseDir = Directory.GetCurrentDirectory();
            Console.WriteLine($"Базовая директория: {baseDir}");
            Console.WriteLine("Введи через пробел имена директорий (пусто = все подкаталоги):");

            var input = (Console.ReadLine() ?? string.Empty).Trim();

            List<DirectoryInfo> targetDirs;
            if (string.IsNullOrWhiteSpace(input))
            {
                // Все подкаталоги (первого уровня) текущей директории, кроме bin/obj
                targetDirs = new DirectoryInfo(baseDir)
                    .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                    .Where(d => !IsExcludedDir(d.Name))
                    .ToList();
            }
            else
            {
                var names = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                targetDirs = new List<DirectoryInfo>(names.Length);
                foreach (var name in names)
                {
                    var fullPath = Path.IsPathRooted(name) ? name : Path.Combine(baseDir, name);

                    if (!Directory.Exists(fullPath))
                    {
                        Console.WriteLine($"[!] Папка не найдена: {fullPath}");
                        continue;
                    }

                    var di = new DirectoryInfo(fullPath);

                    // если пользователь зачем-то указал bin/obj, то тоже игнорируем
                    if (IsExcludedDir(di.Name))
                    {
                        Console.WriteLine($"[!] Папка пропущена (исключение): {di.FullName}");
                        continue;
                    }

                    targetDirs.Add(di);
                }
            }

            if (targetDirs.Count == 0)
            {
                Console.WriteLine("Нечего обрабатывать: список папок пуст.");
                return 0;
            }

            foreach (var dir in targetDirs)
            {
                ProcessDirectory(dir);
            }

            Console.WriteLine("Готово.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка: " + ex);
            return 1;
        }
    }

    private static void ProcessDirectory(DirectoryInfo dir)
    {
        // Выходной файл создаём внутри обрабатываемой подпапки
        // (если хочешь рядом с exe или в корне, поменяй путь здесь)
        //var outFilePath = Path.Combine(dir.FullName, $"{dir.Name}.txt");
        var outFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"{dir.Name}.txt");


        var csFiles = EnumerateCsFilesSafe(dir.FullName).ToList();

        Console.WriteLine($"[{dir.Name}] найдено .cs файлов: {csFiles.Count}");
        Console.WriteLine($"[{dir.Name}] выходной файл: {outFilePath}");

        // Пишем UTF-8 без BOM, чтобы не было сюрпризов
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var fs = new FileStream(outFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(fs, utf8NoBom);

        writer.WriteLine($"Сборка .cs файлов для папки: {dir.FullName}");
        writer.WriteLine($"Дата: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        writer.WriteLine(new string('=', 120));
        writer.WriteLine();

        foreach (var filePath in csFiles)
        {
            writer.WriteLine(new string('#', 120));
            writer.WriteLine($"FILE: {filePath}");
            writer.WriteLine(new string('#', 120));

            try
            {
                // Читаем текстом как есть
                var content = File.ReadAllText(filePath, Encoding.UTF8);
                writer.WriteLine(content);
            }
            catch (Exception ex)
            {
                writer.WriteLine($"[!] Не удалось прочитать файл: {ex.GetType().Name}: {ex.Message}");
            }

            writer.WriteLine();
        }
    }

    private static IEnumerable<string> EnumerateCsFilesSafe(string rootDir)
    {
        // Рекурсивный обход, но с фильтром по папкам и защитой от AccessDenied/битых ссылок
        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var subDir in subDirs)
            {
                var name = Path.GetFileName(subDir);
                if (IsExcludedDir(name))
                    continue;

                stack.Push(subDir);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current, "*.cs", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                yield return Path.GetFullPath(file);
        }
    }

    private static bool IsExcludedDir(string dirName)
        => ExcludedDirNames.Contains(dirName);
}
