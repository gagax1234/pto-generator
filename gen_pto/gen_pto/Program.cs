using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Binding;
using System.CommandLine.Collections;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Suggestions;
using System.CommandLine.Parsing;

namespace gen_pto
{
    class PTO_Template
    {
        public string top;
        public string template_image_first;
        public string templated_image;
        public string bottom;

        public string GetTemplatedImage(string absolute_path)
        {
            return string.Format(templated_image, $"n\"{absolute_path}\"");
        }

        public string GetFirstTemplatedImage(string absolute_path)
        {
            return string.Format(template_image_first, $"n\"{absolute_path}\"");
        }

        public PTO_Template(string template_file)
        {
            string template_string = null;

            using (var fileStream = File.OpenRead(template_file))
            using (var reader = new StreamReader(fileStream))
            {
                template_string = reader.ReadToEnd();
            }

            // terrible code
            int template_index = template_string.IndexOf("<template>");
            top = template_string.Substring(0, template_index);
            top = top.Trim();
            top += "\n";

            int template_end_index = template_string.IndexOf("</template>", template_index);
            templated_image = template_string.Substring(template_index, template_end_index - template_index);
            templated_image = templated_image.Replace("<template>", "");
            templated_image = templated_image.Replace("</template>", "");
            templated_image = templated_image.Trim();
            templated_image += "\n";

            int first_image_index = templated_image.IndexOf("<template_first_image>");
            int first_image_end = templated_image.IndexOf("</template_first_image>");
            template_image_first = templated_image.Substring(first_image_index, first_image_end - first_image_index);
            template_image_first = template_image_first.Replace("<template_first_image>", "");
            template_image_first = template_image_first.Replace("<first_file>", "{0}");
            template_image_first = template_image_first.Trim();
            template_image_first += "\n";

            templated_image = templated_image.Substring(first_image_end);
            templated_image = templated_image.Replace("</template_first_image>", "");
            templated_image = templated_image.Replace("<template_image>", "");
            templated_image = templated_image.Replace("</template_image>", "");
            templated_image = templated_image.Replace("<file>", "{0}");
            templated_image = templated_image.Trim();
            templated_image += "\n";

            bottom = template_string.Substring(template_end_index);
            bottom = bottom.Replace("</template>", "");
            bottom = bottom.Trim();
        }
    }

    class Program
    {
        public static string StartBatchTemplate { get; set; }

        static void CreateUnwarpBatchFile(string output_dir, string prefix, string pto_file)
        {
            string batch_content = StartBatchTemplate.Replace("<prefix>", prefix);
            batch_content = batch_content.Replace("<working_dir>", "./unwarp");
            batch_content = batch_content.Replace("<pto_file>", pto_file);
            
            using (var startFile = File.Open(Path.Combine(output_dir, "unwarp.bat"), FileMode.Create))
            using (var writer = new StreamWriter(startFile))
            {
                writer.Write(batch_content);
            }
        }

        static void CreateRewarpBatchFile(string output_dir, string prefix, string pto_file)
        {
            string batch_content = StartBatchTemplate.Replace("<prefix>", prefix);
            batch_content = batch_content.Replace("<working_dir>", "./rewarp");
            batch_content = batch_content.Replace("<pto_file>", pto_file);

            using (var startFile = File.Open(Path.Combine(output_dir, "rewarp.bat"), FileMode.Create))
            using (var writer = new StreamWriter(startFile))
            {
                writer.Write(batch_content);
            }
        }

        static void Run(string input_path, string output_path, int batch_size)
        {
            using (var fileStream = File.OpenRead("template_start_nona.bat"))
            using (var reader = new StreamReader(fileStream))
            {
                StartBatchTemplate = reader.ReadToEnd();
            }
            var template = new PTO_Template("template.pto_template");
            var reverse_template = new PTO_Template("reverse.pto_template");


            string[] files = Directory.GetFiles(input_path, "*.png");

            int batch_counter = 0;
            while(files.Length > 0)
            {
                string[] batch = files.Take(batch_size).ToArray();
                files = files.Skip(batch_size).ToArray();

                // new batch
                string batch_dir = Path.Combine(output_path, $"batch{batch_counter}");
                Directory.CreateDirectory(batch_dir);

                string unwarp_dir = Path.Combine(batch_dir, "unwarp");
                Directory.CreateDirectory(unwarp_dir);

                string rewarp_dir = Path.Combine(batch_dir, "rewarp");
                Directory.CreateDirectory(rewarp_dir);


                string pto_file_name = $"batch{batch_counter}.pto";
                string pto_file_path = Path.Combine(unwarp_dir, pto_file_name);

                string rewarp_pto_file_name = $"reverse_batch{batch_counter}.pto";
                string rewarp_pto_file_path = Path.Combine(rewarp_dir, rewarp_pto_file_name);

                string prefix = $"{Path.GetFileNameWithoutExtension(batch[0])}_";

                using (var batchFileHandle = File.Open(pto_file_path, FileMode.Create))
                using (var writer = new StreamWriter(batchFileHandle))
                {
                    CreateUnwarpBatchFile(batch_dir, prefix, pto_file_name);
                    
                    Console.WriteLine($"Generating: {pto_file_name}...");

                    writer.Write(template.top);
                    writer.Write(template.GetFirstTemplatedImage(batch[0]));
                    for (int x = 1; x < batch.Length; x++)
                    {
                        writer.Write(template.GetTemplatedImage(batch[x]));
                    }
                    writer.Write("\n");
                    writer.Write(template.bottom);
                }

                // nona.exe enumerates like this $prefix%04d.png
                using (var batchReverseFileHandle = File.Open(rewarp_pto_file_path, FileMode.Create))
                using (var writer = new StreamWriter(batchReverseFileHandle))
                {
                    CreateRewarpBatchFile(batch_dir, prefix, rewarp_pto_file_name);
                    Console.WriteLine($"Generating: {rewarp_pto_file_name}...");

                    writer.Write(reverse_template.top);
                    string image_path = Path.Combine(unwarp_dir, $"{prefix}{0:D4}.png");
                    writer.Write(reverse_template.GetFirstTemplatedImage(image_path));
                    for(int x = 1; x < batch.Length; x++)
                    {
                        image_path = Path.Combine(unwarp_dir, $"{prefix}{x:D4}.png");
                        writer.Write(reverse_template.GetTemplatedImage(image_path));
                    }
                }

                batch_counter += batch_size;
            }
        }

        static void Main(string[] args)
        {
            var rootCommand = new RootCommand(@"^_^")
            {
                new Option<string>(new string[]{ "-i", "--input" }, "Directory which contains split input images.") { Required = true, Argument = new Argument<string>("Path") },
                new Option<string>(new string[]{ "-o", "--output" }, "Directory where batches are generated.") { Required = true, Argument = new Argument<string>("Path")},
                new Option<int>(new string[]{ "-b", "--batchsize" }, () => 1000, "Batch size to generate. (default=1000)") { Required = false, Argument = new Argument<int>("Size", () => 1000) }
            };
            rootCommand.Handler = CommandHandler.Create((string input, string output, int batchsize) => 
            {
                if (batchsize <= 0) return;
                Run(input, output, batchsize);
            });
            rootCommand.TreatUnmatchedTokensAsErrors = true;
            rootCommand.InvokeAsync(args);
        }
    }
}
