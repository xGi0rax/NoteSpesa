using System;
using System.Linq;
using System.Web.Http;
using ErgonApi.Models;
using ErgonApi.Code;
using System.Web;
using Serilog;
using System.Collections.Generic;
using System.IO;
using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Identity;
using System.Text.Json;
using System.Threading.Tasks;
using System.Configuration;

namespace ErgonApi.Controllers
{
    [RoutePrefix("api/note-spesa")]
    public class NoteSpesaController : ApiController
    {
        [HttpPost]
        [Route("")]
        public IHttpActionResult Scarica([FromBody] Parametro model)
        {
            if (model?.credenziali == null)
            {
                return BadRequest("Dati di autenticazione mancanti.");
            }

            try
            {
                Log.Information($"Richiesta lettura note spesa per utente: {model.credenziali.login}");

                if (!int.TryParse(model.credenziali.login, out int cod_dip))
                {
                    return BadRequest("Il login non è valido.");
                }

                // Controllo autenticazione
                if (!Utils.AuthenticateRequest(HttpContext.Current.Request)) return Unauthorized();

                using (Db.ergdisEntities ergEnt = new Db.ergdisEntities())
                {
                    IQueryable<Db.app_note_spesa> query = ergEnt.app_note_spesa.AsNoTracking();

                    var utente = ergEnt.utentiwebs.AsNoTracking().Where(x => 
                        x.login == model.credenziali.login &&
                        x.password == model.credenziali.password &&
                        x.tipo_utente == "LDI").FirstOrDefault();
                    
                    if (utente == null) return Unauthorized();

                    // Lista id note da sincronizzare
                    List<int> keysToSync = new List<int>();

                    if (model.from.HasValue)
                    {
                        Log.Information($"Richiesta incrementale Note Spesa dal: {model.from}");
                        DateTime dataLimite = model.from.Value;

                        // Cerco nella tabella ALOG tutti gli ID nota che hanno subito variazioni dopo dataLimite
                        keysToSync = ergEnt.alog_note_spesa
                            .AsNoTracking()
                            .Where(x => x.cod_dip == cod_dip && x.data_ora_mod > dataLimite)
                            .Select(x => x.id_nota)
                            .Distinct()
                            .ToList();

                        if (keysToSync.Count == 0)
                        {
                            return Ok(new { keys = keysToSync, values = new List<object>() });
                        }

                        // Ricavo le note dalla tabella app_note_spesa
                        query = query.Where(x => keysToSync.Contains(x.id));
                    }
                    else
                    {
                        Log.Information("Richiesta Note Spesa completa (full sync)");
                        query = query.Where(x => x.cod_dip == cod_dip);
                    }

                    var rawResultList = query.Select(x => new
                    {
                        x.id,
                        x.cod_dip,
                        x.cod_cli,
                        x.da_data,
                        x.a_data,
                        x.tipologia,
                        x.tipo_tab_tns,
                        x.nr_dip_ergon,
                        x.flag_con_cli,
                        x.foto_scontrino,
                        x.importo,
                        x.divisa,
                        x.flag_tipo_pag,
                        x.nr_doc_scontrino,
                        x.data_scontrino,
                        x.rag_soc_scontrino,
                        x.note,
                        x.flag_cont,
                        x.partita_iva
                    }).ToList();

                    var tipiNotaSpesa = ergEnt.tabgens.AsNoTracking().Where(x => x.tipo_tab == "TNS").ToList();

                    var resultList = rawResultList.Select(x => new
                    {
                        x.id,
                        x.cod_dip,
                        x.cod_cli,
                        x.da_data,
                        x.a_data,
                        tipologia = tipiNotaSpesa.FirstOrDefault(t => t.cod_tab == x.tipologia)?.des_cod.Trim() ?? x.tipologia,
                        x.tipo_tab_tns,
                        x.nr_dip_ergon,
                        x.flag_con_cli,
                        x.foto_scontrino,
                        x.importo,
                        x.divisa,
                        flag_tipo_pag = GetDescrizionePagamento(x.flag_tipo_pag) ?? x.flag_tipo_pag,
                        x.nr_doc_scontrino,
                        x.data_scontrino,
                        x.rag_soc_scontrino,
                        x.note,
                        x.flag_cont,
                        x.partita_iva
                    }).ToList();

                    if (!model.from.HasValue)
                    {
                        keysToSync = resultList.Select(x => x.id).ToList();
                    }

                    // Ritorno id note da aggiornare e risultati
                    Log.Information($"Sincronizzazione completata: {resultList.Count} record inviati.");
                    return Ok(new { keys = keysToSync, values = resultList });
                }
            }
            catch (Exception e)
            {
                string realError = ExtractRealError(e);
                Log.Error($"Errore lettura note spesa personali: {realError}");
                return InternalServerError();
            }
        }

        [HttpPost]
        [Route("decodifiche")]
        public IHttpActionResult Decodifiche([FromBody] Parametro model) // Endpoint per recuperare i valori le tipologie e i metodi di pagamento
        {
            if (model?.credenziali == null)
            {
                return BadRequest("Dati di autenticazione mancanti.");
            }

            try
            {
                if (!Utils.AuthenticateRequest(HttpContext.Current.Request)) return Unauthorized();

                using (Db.ergdisEntities ergEnt = new Db.ergdisEntities())
                {
                    var utente = ergEnt.utentiwebs.AsNoTracking().FirstOrDefault(x =>
                        x.login == model.credenziali.login &&
                        x.password == model.credenziali.password &&
                        x.tipo_utente == "LDI");

                    if (utente == null) return Unauthorized();

                    // 1. Recupero le tipologie dalla tabella tabgens
                    var tipologie = ergEnt.tabgens.AsNoTracking()
                        .Where(x => x.tipo_tab == "TNS")
                        .Select(x => new { Descrizione = x.des_cod.Trim() })
                        .ToList();

                    // 2. Metodi di pagamento
                    var metodiPagamento = new[]
                    {
                        new { Descrizione = "Carta di credito aziendale" },
                        new { Descrizione = "Carta di credito personale" },
                        new { Descrizione = "Contanti" }
                    }.ToList();

                    return Ok(new { Tipologie = tipologie, MetodiPagamento = metodiPagamento });
                }
            }
            catch (Exception e)
            {
                Log.Error($"Errore lettura decodifiche: {ExtractRealError(e)}");
                return InternalServerError();
            }
        }

        [HttpPost]
        [Route("add")]
        public IHttpActionResult Inserisci([FromBody] Parametro model)
        {
            if (model?.credenziali == null)
            {
                return BadRequest("Dati di autenticazione mancanti.");
            }
            try
            {
                Log.Information($"Arrivata richiesta inserimento note spesa: {model.credenziali.login}");

                if (!Utils.AuthenticateRequest(HttpContext.Current.Request)) return Unauthorized();

                // Controllo utente test
                if (model.credenziali.login == "997")
                {
                    Log.Information("RICHIESTA UTENTE DI TEST 997 - Simulo successo");
                    return Ok();
                }

                if (model.note_spesa == null || !model.note_spesa.Any())
                {
                    Log.Warning("Richiesta ricevuta con lista note_spesa nulla o vuota.");
                    return BadRequest("Nessuna nota spesa fornita.");
                }

                // Validazione: tutte le note devono appartenere allo stesso dipendente
                var dips = model.note_spesa.Select(x => x.cod_dip).Distinct();
                if (dips.Count() != 1) return BadRequest("Dipendenti multipli non ammessi");
                int cod_dip = dips.First();

                using (Db.ergdisEntities ergEnt = new Db.ergdisEntities())
                {
                    // Verifica credenziali utente
                    var utente = ergEnt.utentiwebs.AsNoTracking().Where(x => x.login == model.credenziali.login &&
                                                                             x.password == model.credenziali.password &&
                                                                             x.cod_utente == cod_dip.ToString() &&
                                                                             x.tipo_utente == "LDI").FirstOrDefault();

                    if (utente == null) return Unauthorized();

                    // Recupero parametri e tipi di nota spesa validi
                    var param = ergEnt.app_param.FirstOrDefault();
                    string rootPath = param?.path_scontrini;

                    var tipiNotaSpesa = ergEnt.tabgens.AsNoTracking()
                                                      .Where(x => x.tipo_tab == "TNS")
                                                      .ToList();

                    // Inizio transazione per salvataggi multipli
                    using (var transaction = ergEnt.Database.BeginTransaction())
                    {
                        List<string> fileScrittiInTransazione = new List<string>();

                        try
                        {
                            var base64Photos = new Dictionary<Db.app_note_spesa, string>();

                            foreach (SpesaDettaglio s in model.note_spesa)
                            {
                                var rigaTabgen = tipiNotaSpesa.FirstOrDefault(x =>
                                        x.des_cod.Trim().ToUpper() == s.tipologia.Trim().ToUpper());

                                if (rigaTabgen == null)
                                {
                                    Log.Warning($"Inserimento annullato. Tipologia nota spesa non riconosciuta: '{s.tipologia}' per dipendente {cod_dip}");
                                    return BadRequest($"La tipologia di spesa '{s.tipologia}' non è valida o non è censita a sistema. Operazione annullata.");
                                }

                                Db.app_note_spesa entry = new Db.app_note_spesa()
                                {
                                    cod_dip = cod_dip,
                                    cod_cli = s.cod_cli,
                                    da_data = s.da_data,
                                    a_data = s.a_data,
                                    tipologia = rigaTabgen.cod_tab,
                                    tipo_tab_tns = rigaTabgen.tipo_tab,
                                    nr_dip_ergon = s.nr_dip_ergon,
                                    flag_con_cli = s.flag_con_cli == "S" ? "S" : "N",
                                    foto_scontrino = null,
                                    importo = s.importo,
                                    flag_tipo_pag = GetCodicePagamento(s.flag_tipo_pag),
                                    nr_doc_scontrino = s.nr_doc_scontrino,
                                    data_scontrino = s.data_scontrino,
                                    rag_soc_scontrino = s.rag_soc_scontrino,
                                    partita_iva = s.partita_iva,
                                    note = s.note,
                                    flag_cont = s.flag_cont
                                };

                                ergEnt.app_note_spesa.Add(entry);

                                // Salvo le foto dello scontrino in un dizionario
                                if (!string.IsNullOrEmpty(s.foto_scontrino))
                                    base64Photos.Add(entry, s.foto_scontrino);
                            }
                            
                            ergEnt.SaveChanges(); // Primo salvataggio

                            // Seconda fase, salvataggio fisico della foto e aggiornamento path
                            if (!string.IsNullOrEmpty(rootPath))
                            {
                                foreach (var item in base64Photos)
                                {
                                    var entry = item.Key;
                                    var rawBase64 = item.Value;

                                    if (!IsValidImageBase64(rawBase64, out byte[] imageBytes, out string extension))
                                    {
                                        Log.Warning($"Validazione foto fallita per la nota spesa ID {entry.id}. Il file verrà saltato.");
                                        continue;
                                    }
                                    try
                                    {
                                        string subFolder = entry.da_data.ToString("yyyy_MM");
                                        string targetDirectory = Path.Combine(rootPath, subFolder);
                                        if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                                        // Il nome del file è composto da idRecord.estensione
                                        string fileName = $"{entry.id}{extension}";
                                        string physicalPath = Path.Combine(targetDirectory, fileName);

                                        File.WriteAllBytes(physicalPath, imageBytes);
                                        fileScrittiInTransazione.Add(physicalPath);

                                        entry.foto_scontrino = $"/{subFolder}/{fileName}";
                                        Log.Information($"Foto salvata correttamente per ID {entry.id}: {physicalPath}");
                                    }
                                    catch (Exception exFile)
                                    {
                                        Log.Error($"Errore salvataggio file per ID {entry.id}: {exFile.Message}");
                                        throw new Exception($"Impossibile salvare l'allegato fotografico per la spesa del {entry.da_data:dd/MM/yyyy}. L'intera operazione è stata annullata.");
                                    }
                                }
                                ergEnt.SaveChanges();  // Secondo salvataggio
                            }
                            // Transazione completata
                            transaction.Commit();
                            Log.Information($"Inserimento completato con successo. Dipendente: {cod_dip}");
                            return Ok();
                        }
                        catch (Exception exTx)
                        {
                            // Qualcosa è andato storto, rollback
                            transaction.Rollback();
                            Log.Error($"Errore durante la transazione. Rollback eseguito. Errore: {exTx.Message}");

                            foreach (var filePath in fileScrittiInTransazione)
                            {
                                try
                                {
                                    if (File.Exists(filePath)) File.Delete(filePath);
                                }
                                catch (Exception exDel)
                                {
                                    Log.Error($"Impossibile eliminare il file orfano post-rollback {filePath}: {exDel.Message}");
                                }
                            }
                            throw;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                string realError = ExtractRealError(e);
                Log.Error($"ERRORE INSERIMENTO: {realError}");
                return BadRequest($"Errore database: {realError}");
            }
        }

        [HttpPost]
        [Route("update")]
        public IHttpActionResult Modifica([FromBody] Parametro model)
        {
            if (model == null || model.credenziali == null || model.note_spesa == null || !model.note_spesa.Any())
            {
                return BadRequest("Dati non validi.");
            }

            try
            {
                Log.Information($"Richiesta modifica nota spesa: {model.credenziali.login}");

                if (!Utils.AuthenticateRequest(HttpContext.Current.Request)) return Unauthorized();

                using (Db.ergdisEntities ergEnt = new Db.ergdisEntities())
                {
                    // Verifica credenziali
                    var utente = ergEnt.utentiwebs.AsNoTracking().Where(x => x.login == model.credenziali.login &&
                                                                           x.password == model.credenziali.password &&
                                                                           x.tipo_utente == "LDI").FirstOrDefault();
                    if (utente == null) return Unauthorized();

                    // Recupero parametri
                    var param = ergEnt.app_param.FirstOrDefault();
                    var tipiNotaSpesa = ergEnt.tabgens.AsNoTracking().Where(x => x.tipo_tab == "TNS").ToList();
                    string rootPath = param?.path_scontrini;

                    if (string.IsNullOrEmpty(rootPath))
                    {
                        Log.Warning("Attenzione: rootPath nullo, il salvataggio delle foto verrà saltato.");
                    }
                    
                    bool recordTrovato = false;

                    foreach (var s in model.note_spesa)
                    {
                        // Cerco il record esistente nel database tramite l'ID
                        var entry = ergEnt.app_note_spesa.FirstOrDefault(x => x.id == s.id);
                        if (entry == null)
                        {
                            Log.Warning($"Tentativo di modifica record inesistente. ID: {s.id}");
                            continue; // Salta alla prossima nota
                        }

                        recordTrovato = true;

                        // Verifico che la nota appartenga effettivamente all'utente loggato
                        if (entry.cod_dip.ToString() != model.credenziali.login) return Unauthorized();

                        var rigaTabgen = tipiNotaSpesa.FirstOrDefault(x => x.des_cod.Trim().ToUpper() == s.tipologia.Trim().ToUpper());

                        // Se è una categoria non valida blocco l'operazione
                        if (rigaTabgen == null)
                        {
                            Log.Warning($"Modifica annullata. Tipologia nota spesa non riconosciuta: '{s.tipologia}' per record ID {s.id}");
                            return BadRequest($"La tipologia di spesa '{s.tipologia}' non è valida. Operazione annullata.");
                        }

                        // Aggiornamento campi
                        entry.cod_cli = s.cod_cli;
                        entry.da_data = s.da_data;
                        entry.a_data = s.a_data;
                        entry.nr_dip_ergon = s.nr_dip_ergon;
                        entry.flag_con_cli = s.flag_con_cli == "S" ? "S" : "N";
                        entry.importo = s.importo;
                        entry.flag_tipo_pag = GetCodicePagamento(s.flag_tipo_pag);
                        entry.nr_doc_scontrino = s.nr_doc_scontrino;
                        entry.data_scontrino = s.data_scontrino;
                        entry.rag_soc_scontrino = s.rag_soc_scontrino;
                        entry.partita_iva = s.partita_iva;
                        entry.note = s.note;

                        if (rigaTabgen != null)
                        {
                            entry.tipologia = rigaTabgen.cod_tab;
                            entry.tipo_tab_tns = rigaTabgen.tipo_tab;
                        }

                        // Gestione nuova foto
                        if (!string.IsNullOrEmpty(rootPath) && !string.IsNullOrEmpty(s.foto_scontrino) && s.foto_scontrino.Length > 100)
                        {
                            if (!IsValidImageBase64(s.foto_scontrino, out byte[] imageBytes, out string extension))
                            {
                                Log.Warning($"Validazione foto fallita per la modifica della nota spesa ID {entry.id}. Aggiornamento immagine annullato.");
                            }
                            else
                            {
                                try
                                {
                                    string subFolder = entry.da_data.ToString("yyyy_MM");
                                    string targetDirectory = Path.Combine(rootPath, subFolder);

                                    if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                                    string fileName = $"{entry.id}{extension}";
                                    string physicalPath = Path.Combine(targetDirectory, fileName);
                                    string newRelativePath = $"/{subFolder}/{fileName}";

                                    if (!string.IsNullOrEmpty(entry.foto_scontrino) && entry.foto_scontrino != newRelativePath)
                                    {
                                        try
                                        {
                                            string oldPhysicalPath = Path.Combine(rootPath, entry.foto_scontrino.TrimStart('/'));
                                            if (File.Exists(oldPhysicalPath)) File.Delete(oldPhysicalPath);
                                        }
                                        catch (Exception exDel)
                                        {
                                            Log.Warning($"Impossibile eliminare vecchio file {entry.foto_scontrino}: {exDel.Message}");
                                        }
                                    }
                                    File.WriteAllBytes(physicalPath, imageBytes);

                                    entry.foto_scontrino = newRelativePath;

                                    Log.Information($"Foto aggiornata correttamente per record {entry.id}: {newRelativePath}");
                                }
                                catch (Exception exFile)
                                {
                                    Log.Error($"Errore critico gestione file per ID {entry.id}: {exFile.Message}");
                                }
                            }
                        }
                        else if(s.foto_scontrino == "CANCELLATA")
                        {
                            if(!string.IsNullOrEmpty(entry.foto_scontrino) && !string.IsNullOrEmpty(rootPath))
                            {
                                try
                                {
                                    string oldPhysicalPath = Path.Combine(rootPath, entry.foto_scontrino.TrimStart('/'));
                                    if (File.Exists(oldPhysicalPath))
                                    {
                                        File.Delete(oldPhysicalPath);
                                        Log.Information($"Foto eliminata fisicamente dal server per riga spesa ID {entry.id}: {oldPhysicalPath}");
                                    }
                                }
                                catch(Exception exDel)
                                {
                                    Log.Warning($"Impossibile eliminare file rimosso dal server {entry.foto_scontrino}: {exDel.Message}");
                                }
                            }
                            entry.foto_scontrino = null;
                        }
                    }
                    if (!recordTrovato)
                    {
                        return BadRequest("Nessun record corrispondente trovato sul server.");
                    }

                    ergEnt.SaveChanges();
                    return Ok();
                }
            }
            catch (Exception e)
            {
                string realError = ExtractRealError(e);
                Log.Error($"ERRORE MODIFICA: {realError}");
                return BadRequest($"Errore database: {realError}");
            }
        }

        [HttpPost]
        [Route("analyze")]
        public async Task<IHttpActionResult> AnalizzaScontrino([FromBody] Parametro model)
        {
            if (model?.credenziali == null)
            {
                return BadRequest("Dati di autenticazione mancanti.");
            }
            if (model?.richiesta_analisi == null)
            {
                return BadRequest("Nessuna immagine fornita.");
            }

            try
            {
                Log.Information("Inizio analisi scontrino...");

                // Validazione immagine
                if(!IsValidImageBase64(model.richiesta_analisi.FotoBase64, out byte[] imageBytes, out string extension))
                {
                    return BadRequest("Formato immagine non valido o dimensione eccessiva.");
                }

                // Recupero configurazioni
                string endpoint = ConfigurationManager.AppSettings["AzureAI:Endpoint"];
                string key = ConfigurationManager.AppSettings["AzureAI:Key"];
                string analyzerId = ConfigurationManager.AppSettings["AzureAI:AnalyzerId"];
                //string apiVersion = ConfigurationManager.AppSettings["AzureAI:ApiVersion"];

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(endpoint))
                {
                    return InternalServerError(new Exception("Configurazione IA mancante sul server."));
                }

                var clientOptions = new ContentUnderstandingClientOptions(ContentUnderstandingClientOptions.ServiceVersion.V2025_11_01);
                var client = new ContentUnderstandingClient(new Uri(endpoint), new AzureKeyCredential(key), clientOptions);

                // Chiamata ad Azure
                using (var stream = new MemoryStream(imageBytes))
                {
                    var inputs = new List<AnalysisInput>() { new AnalysisInput { Data = BinaryData.FromStream(stream) } };

                    Operation<AnalysisResult> operation = await client.AnalyzeAsync(
                        WaitUntil.Completed,
                        analyzerId,
                        inputs);

                    // Deserializzazione risultato
                    string resultJson = JsonSerializer.Serialize(operation.Value);

                    ScontrinoEstratto datiEstratti = new ScontrinoEstratto();

                    using (JsonDocument doc = JsonDocument.Parse(resultJson))
                    {
                        JsonElement root = doc.RootElement;

                        if(root.TryGetProperty("Contents", out JsonElement contentsArray) && contentsArray.GetArrayLength() > 0)
                        {
                            if(contentsArray[0].TryGetProperty("Fields", out JsonElement fields))
                            {
                                string GetFieldValue(string fieldName)
                                {
                                    if(fields.TryGetProperty(fieldName, out JsonElement fieldObj) &&
                                        fieldObj.TryGetProperty("Value", out JsonElement valueElement))
                                    {
                                        return valueElement.ValueKind != JsonValueKind.Null ? valueElement.ToString() : null;
                                    }
                                    return null;
                                }

                                // Estrazione dati
                                datiEstratti.NumeroDocumento = GetFieldValue("nr_doc");
                                datiEstratti.RagioneSociale = GetFieldValue("rag_soc");
                                datiEstratti.PartitaIva = GetFieldValue("partita_iva");

                                string dataString = GetFieldValue("data_doc");
                                string oraString = GetFieldValue("ora_doc");
                                datiEstratti.OraDocumento = oraString;

                                if (DateTime.TryParse(dataString, out DateTime parsedDate))
                                {
                                    DateTime dataPura = new DateTime(
                                        parsedDate.Year,
                                        parsedDate.Month,
                                        parsedDate.Day,
                                        0, 0, 0,
                                        DateTimeKind.Unspecified);

                                    if (!string.IsNullOrWhiteSpace(oraString) && TimeSpan.TryParse(oraString, out TimeSpan parsedTime))
                                    {
                                        datiEstratti.DataDocumento = dataPura.Add(parsedTime);
                                    }
                                    else
                                    {
                                        datiEstratti.DataDocumento = dataPura;
                                    }
                                }

                                string importoString = GetFieldValue("importo_ivato");
                                if(double.TryParse(importoString, 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out double parsedImporto))
                                {
                                    datiEstratti.ImportoTotale = parsedImporto;
                                }
                            }
                        }
                    }

                    bool haDati = !string.IsNullOrWhiteSpace(datiEstratti.NumeroDocumento) ||
                                  !string.IsNullOrWhiteSpace(datiEstratti.RagioneSociale) ||
                                  !string.IsNullOrWhiteSpace(datiEstratti.PartitaIva) ||
                                  datiEstratti.DataDocumento.HasValue ||
                                  datiEstratti.ImportoTotale.HasValue;

                    if (!haDati)
                    {
                        Log.Warning("L'AI ha processato l'immagine ma non ha estratto alcun campo utile.");
                        return Ok((ScontrinoEstratto)null);
                    }

                    Log.Information("Analisi completata con successo, trovato almeno un campo valido.");
                    return Ok(datiEstratti);
                }
            }
            catch (RequestFailedException ex)
            {
                Log.Error($"Errore Azure Content Understanding: {ex.Status} - {ex.Message}");
                return BadRequest("Impossibile analizzare lo scontrino al momento.");
            }
            catch (Exception e)
            {
                Log.Error($"Errore imprevisto durante l'analisi: {ExtractRealError(e)}");
                return InternalServerError();
            }
        }

        private string GetCodicePagamento(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return "";
            switch (desc.ToLower())
            {
                case "carta di credito aziendale": return "CC";
                case "carta di credito personale": return "CP";
                case "contanti": return "CO";
                default: return "";
            }
        }

        private string GetDescrizionePagamento(string cod)
        {
            if (string.IsNullOrEmpty(cod)) return "";
            switch (cod.ToUpper())
            {
                case "CC": return "Carta di credito aziendale";
                case "CP": return "Carta di credito personale";
                case "CO": return "Contanti";
                default: return cod;
            }
        }

        private bool IsValidImageBase64(string rawBase64, out byte[] imageBytes, out string extension)
        {
            imageBytes = null;
            extension = null;

            if (string.IsNullOrEmpty(rawBase64)) return false;

            // Pulizia prefisso Data URI
            string cleanBase64 = rawBase64.Contains(",") ? rawBase64.Split(',')[1] : rawBase64;

            // Controllo dimensione
            long estimatedSizeInBytes = (cleanBase64.Length * 3) / 4;
            long maxAllowedSize = 5 * 1024 * 1024; // Limite 5MB

            if (estimatedSizeInBytes > maxAllowedSize)
            {
                Log.Warning($"Upload rifiutato: il file supera il limite massimo di 5MB (Dimensione stimata: {estimatedSizeInBytes} bytes).");
                return false;
            }

            try
            {
                imageBytes = Convert.FromBase64String(cleanBase64);

                if (imageBytes.Length < 4) return false;

                // Controllo i primi byte per determinare il vero tipo di file
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                {
                    extension = ".jpg";
                }
                else if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                {
                    extension = ".png";
                }
                else if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x38)
                {
                    extension = ".gif";
                }
                else if (imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46)
                {
                    extension = ".webp";
                }
                else if (imageBytes[0] == 0x25 && imageBytes[1] == 0x50 && imageBytes[2] == 0x44 && imageBytes[3] == 0x46)
                {
                    extension = ".pdf";
                }

                return extension != null;
            }
            catch (Exception ex)
            {
                Log.Error($"Errore durante la decodifica e validazione del Base64: {ex.Message}");
                return false;
            }
        }

        private string ExtractRealError(Exception e)
        {
            if (e is System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                return string.Join("; ", dbEx.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage));
            }
            var msg = e.Message;
            if (e.InnerException != null)
            {
                msg = e.InnerException.Message;
                if (e.InnerException.InnerException != null) msg = e.InnerException.InnerException.Message;
            }
            return msg;
        }

    }
}