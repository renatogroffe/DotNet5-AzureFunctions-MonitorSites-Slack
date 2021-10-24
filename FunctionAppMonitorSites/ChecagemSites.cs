using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionAppMonitorSites
{
    public static class ChecagemSites
    {
        [Function("ChecagemSites")]
        public static void Run([TimerTrigger("0 */1 * * * *")] FunctionContext context)
        {
            var logger = context.GetLogger("ChecagemSites");

            logger.LogInformation(
                $"Iniciando execucao em: {DateTime.Now:HH:mm:ss}");

            var hosts = Environment.GetEnvironmentVariable("SitesMonitoramento")
                .Split("|", StringSplitOptions.RemoveEmptyEntries);
            foreach (string host in hosts)
            {
                logger.LogInformation(
                    $"Verificando a disponibilidade do host {host}");


                string descricaoErro = null;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();

                    try
                    {
                        // Envio da requisicao a fim de determinar se
                        // o site esta no ar
                        HttpResponseMessage response =
                            client.GetAsync(host).Result;
                        
                        var statusCode =(int)response.StatusCode + " " +
                            response.StatusCode;
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            descricaoErro = $"Status {statusCode}";
                        else
                            logger.LogInformation($"{host}: Status {statusCode}");
                    }
                    catch (Exception ex)
                    {
                        descricaoErro = $"Exception {ex.Message}";
                    }
                }

                if (descricaoErro is not null)
                {
                    logger.LogError($"{host} - Falha: {descricaoErro}");
                    var urlLogicApp = Environment.GetEnvironmentVariable("UrlLogicAppAlerta");
                    if (!String.IsNullOrWhiteSpace(urlLogicApp))
                    {
                        using var clientLogicAppSlack = new HttpClient();
                        clientLogicAppSlack.BaseAddress = new Uri(urlLogicApp);
                        clientLogicAppSlack.DefaultRequestHeaders.Accept.Clear();
                        clientLogicAppSlack.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));

                        var requestMessage =
                              new HttpRequestMessage(HttpMethod.Post, String.Empty);

                        requestMessage.Content = new StringContent(
                            JsonSerializer.Serialize(new
                            {
                                site = host,
                                erro = descricaoErro
                            }), Encoding.UTF8, "application/json");

                        var respLogicApp = clientLogicAppSlack
                            .SendAsync(requestMessage).Result;
                        respLogicApp.EnsureSuccessStatusCode();

                        logger.LogInformation(
                            "Envio de alerta para Logic App de integração com o Slack");
                    }
                }
            }

            logger.LogInformation(
                $"Execucao concluida em: {DateTime.Now:HH:mm:ss}");
        }
    }
}