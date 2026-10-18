using System;
using System.IO;
using System.Text;

namespace SelectML.Core
{
    /// <summary>
    /// Utilitário de alta performance para detecção automática de codificação de arquivos de texto.
    /// Distingue de forma transparente entre UTF-8 (com ou sem BOM) e ISO-8859-1 (Latin-1 / ANSI),
    /// garantindo a preservação íntegra de símbolos industriais como 'Ø', '°', 'µ' e acentuações.
    /// </summary>
    public static class EncodingDetector
    {
        public static Encoding Detect(string filePath)
        {
            if (!File.Exists(filePath))
                return Encoding.UTF8;

            byte[] buffer = new byte[64 * 1024]; // Analisa os primeiros 64KB
            int bytesRead;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = fs.Read(buffer, 0, buffer.Length);
            }

            if (bytesRead == 0)
                return Encoding.UTF8;

            // 1. Verificação de BOM de 4 bytes (UTF-32)
            if (bytesRead >= 4)
            {
                if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                    return Encoding.UTF32; // UTF-32 LE

                if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
                    return new UTF32Encoding(true, true); // UTF-32 BE
            }

            // 2. Verificação de BOM de 3 bytes (UTF-8)
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return Encoding.UTF8;

            // 3. Verificação de BOM de 2 bytes (UTF-16)
            if (bytesRead >= 2)
            {
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.Unicode; // UTF-16 LE

                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode; // UTF-16 BE
            }

            // 4. Heurística estatística para detecção de UTF-16 LE ou BE sem BOM
            // Arquivos industriais emitidos sem BOM contêm códigos de caracteres ocidentais onde o byte alto é 0x00.
            // Para UTF-16 LE: os bytes ímpares (índices 1, 3, 5...) concentram os 0x00 e os pares contêm dados.
            // Para UTF-16 BE: os bytes pares (índices 0, 2, 4...) concentram os 0x00 e os ímpares contêm dados.
            int sampleSize = Math.Min(bytesRead, 2048);
            if (sampleSize >= 4)
            {
                if (sampleSize % 2 != 0) sampleSize--;
                int totalPairs = sampleSize / 2;
                int nullEvens = 0;
                int nullOdds = 0;

                for (int k = 0; k < sampleSize; k += 2)
                {
                    if (buffer[k] == 0) nullEvens++;
                    if (buffer[k + 1] == 0) nullOdds++;
                }

                // UTF-16 LE sem BOM: alta concentração de 0x00 nos bytes ímpares e quase nenhum nos pares
                if ((double)nullOdds / totalPairs >= 0.70 && (double)nullEvens / totalPairs < 0.10)
                {
                    return Encoding.Unicode; // UTF-16 LE
                }

                // UTF-16 BE sem BOM: alta concentração de 0x00 nos bytes pares e quase nenhum nos ímpares
                if ((double)nullEvens / totalPairs >= 0.70 && (double)nullOdds / totalPairs < 0.10)
                {
                    return Encoding.BigEndianUnicode; // UTF-16 BE
                }
            }

            // 5. Análise heurística de UTF-8 vs Latin-1 (ISO-8859-1)
            bool hasMultiByteUtf8 = false;
            bool isPureAscii = true;

            int i = 0;
            while (i < bytesRead)
            {
                byte current = buffer[i];

                if (current <= 0x7F)
                {
                    // ASCII normal (0 - 127)
                    i++;
                    continue;
                }

                isPureAscii = false;

                // Sequência UTF-8 de 2 bytes: 110xxxxx 10xxxxxx (0xC2 a 0xDF)
                if ((current >= 0xC2 && current <= 0xDF) && (i + 1 < bytesRead))
                {
                    byte next = buffer[i + 1];
                    if (next >= 0x80 && next <= 0xBF)
                    {
                        hasMultiByteUtf8 = true;
                        i += 2;
                        continue;
                    }
                    else
                    {
                        // Sequência inválida para UTF-8 -> é Latin-1 (ANSI)
                        return Encoding.Latin1;
                    }
                }

                // Sequência UTF-8 de 3 bytes: 1110xxxx 10xxxxxx 10xxxxxx (0xE0 a 0xEF)
                if ((current >= 0xE0 && current <= 0xEF) && (i + 2 < bytesRead))
                {
                    byte next1 = buffer[i + 1];
                    byte next2 = buffer[i + 2];
                    if ((next1 >= 0x80 && next1 <= 0xBF) && (next2 >= 0x80 && next2 <= 0xBF))
                    {
                        hasMultiByteUtf8 = true;
                        i += 3;
                        continue;
                    }
                    else
                    {
                        return Encoding.Latin1;
                    }
                }

                // Sequência UTF-8 de 4 bytes: 11110xxx ... (0xF0 a 0xF4)
                if ((current >= 0xF0 && current <= 0xF4) && (i + 3 < bytesRead))
                {
                    byte next1 = buffer[i + 1];
                    byte next2 = buffer[i + 2];
                    byte next3 = buffer[i + 3];
                    if ((next1 >= 0x80 && next1 <= 0xBF) && (next2 >= 0x80 && next2 <= 0xBF) && (next3 >= 0x80 && next3 <= 0xBF))
                    {
                        hasMultiByteUtf8 = true;
                        i += 4;
                        continue;
                    }
                    else
                    {
                        return Encoding.Latin1;
                    }
                }

                // Se encontrou byte > 127 que não forma sequência UTF-8 válida, é Latin-1
                return Encoding.Latin1;
            }

            // Se for estritamente ASCII ou tiver sequências UTF-8 válidas, usa UTF-8
            return hasMultiByteUtf8 || isPureAscii ? Encoding.UTF8 : Encoding.Latin1;
        }

        public static string DetectAndReadAllText(string filePath)
        {
            var encoding = Detect(filePath);
            string text = File.ReadAllText(filePath, encoding);
            return SanitizeText(text);
        }

        public static string[] DetectAndReadAllLines(string filePath)
        {
            var encoding = Detect(filePath);
            var lines = File.ReadAllLines(filePath, encoding);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = SanitizeText(lines[i]);
            }
            return lines;
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.IndexOf('\0') >= 0 ? text.Replace("\0", string.Empty) : text;
        }
    }
}
