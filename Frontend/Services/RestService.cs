using Ergon.Models;
using Ergon.Models.Enums;
using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;
using System.Text;

namespace Ergon.Services
{
    public static class RestService
    {
        private static RestClient GetErgClient() 
        {
            var options = new RestClientOptions(new Uri(Constants.URL))
            {
                Timeout = TimeSpan.FromSeconds(45) // Limite di timeout di 45s per le risposte del backend
            };
            return new RestClient(options); 
        }
        private static RestRequest CreateBasicAuthRequest(string resource, Method method)
        {
            RestRequest request = new(resource, method);
            request.AddHeader("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(Constants.BASIC_AUTH))}");
            if (method == Method.Post)
            {
                request.AddHeader("Content-Type", "application/json");
            }
            return request;
        }
        public static async Task<bool> Login(string username, string password)
        {
            try
            {
                RestRequest request = CreateBasicAuthRequest(Constants.LOGIN, Method.Post);
                request.AddBody(new { login = username, password });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                LoginResponse? loginResponse = JsonConvert.DeserializeObject<LoginResponse>(response.Content);
                if (loginResponse == null) return false;

                Settings.IsLogged = true;
                Settings.CodDipendente = loginResponse.cod_dip;
                Settings.DesDipendente = loginResponse.des_dip;
                Settings.DaOraNotturno = loginResponse.da_ora_notturno;
                Settings.AOraNotturno = loginResponse.a_ora_notturno;
                Settings.UltimaVersioneAndroid = loginResponse.vers_android;
                Settings.UltimaVersioneIOS = loginResponse.vers_ios;
                Settings.Username = username;
                Settings.Password = password;

                // Recupero i valori per le note spesa
                await SyncTabelleDecodificaAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> Timbra(List<Timbratura> timbrature)
        {
            try
            {
                if (!Settings.IsLogged) return false;

                object credenziali = new { login = Settings.Username, password = Settings.Password };

                RestRequest request = CreateBasicAuthRequest(Constants.TIMBRATURA, Method.Post);
                request.Method = Method.Post;
                request.AddBody(new { timbrature, credenziali });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                return response.IsSuccessful;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetPlanning(bool aggiorna, bool personale)
        {
            try
            {
                if (!Settings.IsLogged)
                    return false;

                object credenziali = new { login = Settings.Username, password = Settings.Password };
                int? cod_dip = personale ? Settings.CodDipendente : null;
                DateTime? from = aggiorna ? Settings.LastSync.ToLocalTime() : null;
                if (from == DateTime.MinValue)
                    from = null;
                object query_planning = new { cod_dip, from };

                RestRequest request = CreateBasicAuthRequest(Constants.PLANNING, Method.Post);
                request.AddBody(new { credenziali, query_planning });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return false;

                PlanningResponse? planningResponse = JsonConvert.DeserializeObject<PlanningResponse>(response.Content);
                if (planningResponse == null || planningResponse.keys == null || planningResponse.valori == null)
                    return false;

                if (aggiorna) // se non sto aggiornando, le chiavi sono inutili perché il database è vuoto
                {
                    // elimino i record del planning per questi dipendenti e questi giorni
                    foreach (var key in planningResponse.keys)
                    {
                        App.Database.Delete<Planning>(x => x.cod_dip == key.cod_dip && x.data == key.data);
                    }
                }
                else
                {
                    App.Database.DeleteAll<Planning>();
                }

                // inserisco i record aggiornati
                App.Database.InsertAll(planningResponse.valori);

                return true;
            }
            catch (Exception ex)
            {
                string msd = ex.Message;

                return false;
            }
        }
        public static async Task<bool> GetFaq()
        {
            try
            {
                RestRequest request = CreateBasicAuthRequest(Constants.FAQ, Method.Get);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                HtmlResponse? faqResponse = JsonConvert.DeserializeObject<HtmlResponse>(response.Content);
                if (faqResponse == null || faqResponse.html == null) return false;

                Settings.FaqHtml = faqResponse.html;

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetCalendario()
        {
            try
            {
                RestRequest request = CreateBasicAuthRequest(Constants.CALENDARIO, Method.Get);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                HtmlResponse? calendarioResponse = JsonConvert.DeserializeObject<HtmlResponse>(response.Content);
                if (calendarioResponse == null || calendarioResponse.html == null) return false;

                Settings.CalendarioHtml = calendarioResponse.html;

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetTimbrature()
        {
            List<Timbratura> backup = [];
            bool eliminate = false;
            try
            {
                object credenziali = new { login = Settings.Username, password = Settings.Password };

                RestRequest request = CreateBasicAuthRequest(Constants.TIMBRATURE_PERSONALI, Method.Post);
                request.AddBody(credenziali);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                Timbratura[]? timbratureResponse = JsonConvert.DeserializeObject<Timbratura[]>(response.Content);
                if (timbratureResponse == null) return false;

                // prendo tutte le timbrature
                backup = App.Database.GetAll<Timbratura>();

                // le elimino
                App.Database.DeleteAll<Timbratura>();
                eliminate = true;

                // inserisco le timbrature ricevute
                App.Database.InsertAll(timbratureResponse.Where(x => x.cod_dip == Settings.CodDipendente));

                var list = App.Database.GetAll<Timbratura>();

                return true;
            }
            catch
            {
                // se qualcosa è andato male, rimetto le timbrature di prima
                try
                {
                    if (backup != null && backup.Count > 0 && eliminate)
                    {
                        App.Database.DeleteAll<Timbratura>();
                        App.Database.InsertAll(backup);
                    }
                }
                catch { } // teoricamente non dovrebbe mai succedere
            }

            return false;
        }
        public static async Task<bool> GetPresenze()
        {
            try
            {
                object credenziali = new { login = Settings.Username, password = Settings.Password };

                RestRequest request = CreateBasicAuthRequest(Constants.LIBRO_PRESENZE, Method.Post);
                request.AddBody(credenziali);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                Presenza[]? presenzeResponse = JsonConvert.DeserializeObject<Presenza[]>(response.Content);
                if (presenzeResponse == null) return false;

                // le elimino
                App.Database.DeleteAll<Presenza>();

                // inserisco le presenze ricevute
                App.Database.InsertAll(presenzeResponse);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetAnavoci()
        {
            try
            {
                RestRequest request = CreateBasicAuthRequest(Constants.ANAVOCI, Method.Get);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                Anavoci[]? legendaResponse = JsonConvert.DeserializeObject<Anavoci[]>(response.Content);
                if (legendaResponse == null) return false;

                // le elimino
                App.Database.DeleteAll<Anavoci>();

                // inserisco le presenze ricevute
                App.Database.InsertAll(legendaResponse);

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetDipendenti()
        {
            try
            {
                RestRequest request = CreateBasicAuthRequest(Constants.DIPENDENTI, Method.Get);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                Dipendente[]? dipendentiResponse = JsonConvert.DeserializeObject<Dipendente[]>(response.Content);
                if (dipendentiResponse == null) return false;

                // le elimino
                App.Database.DeleteAll<Dipendente>();

                // inserisco i dipendenti ricevuti
                App.Database.InsertAll(dipendentiResponse);

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetTabgen()
        {
            try
            {
                RestRequest request = CreateBasicAuthRequest(Constants.TABGEN, Method.Get);

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                Tabgen[]? tabgenResponse = JsonConvert.DeserializeObject<Tabgen[]>(response.Content);
                if (tabgenResponse == null) return false;

                // le elimino
                App.Database.DeleteAll<Tabgen>();

                // inserisco le tabelle generiche ricevute
                App.Database.InsertAll(tabgenResponse);

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<bool> GetClienti(bool aggiorna)
        {
            try
            {
                if (!Settings.IsLogged) return false;

                object credenziali = new { login = Settings.Username, password = Settings.Password };
                DateTime? from = aggiorna ? Settings.LastSync.ToLocalTime() : null;

                RestRequest request = CreateBasicAuthRequest(Constants.CLIENTI, Method.Post);
                request.AddBody(new { credenziali, from });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content)) return false;

                ClienteResponse? clienteResponse = JsonConvert.DeserializeObject<ClienteResponse>(response.Content);
                if (clienteResponse == null || clienteResponse.keys == null || clienteResponse.valori == null) return false;

                if (aggiorna) // se non sto aggiornando, le chiavi sono inutili perché il database è vuoto
                {
                    // elimino i record del planning per questi dipendenti e questi giorni
                    App.Database.Delete<Cliente>(x => clienteResponse.keys.Contains(x.cod_cli));

                    //foreach (int key in clienteResponse.keys)
                    //{
                    //    App.Database.Delete<Cliente>(x => x.cod_cli == key);
                    //}
                }
                else
                {
                    App.Database.DeleteAll<Cliente>();
                }

                // inserisco i record aggiornati
                App.Database.InsertAll(clienteResponse.valori);

                return true;
            }
            catch (Exception ex)
            {
                string msd = ex.Message;

                return false;
            }
        }

        // NOTE SPESA

        // Metodo per scaricare i valori di Tipologia e Metodi di pagamento dal backend
        public static async Task<bool> SyncTabelleDecodificaAsync()
        {
            if (!Settings.IsLogged) return false;

            object credenziali = new { login = Settings.Username, password = Settings.Password };
            RestRequest request = CreateBasicAuthRequest(Constants.NOTE_SPESA_DECODIFICHE, Method.Post);
            request.AddBody(new { credenziali });

            try
            {
                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content)) return false;

                var result = JsonConvert.DeserializeObject<DecodificheResponse>(response.Content);

                if (result != null)
                {
                    App.Database.BeginTrans();

                    // Svuoto e ripopolo le tabelle locali
                    App.Database.DeleteAll<SpesaTipologia>();
                    App.Database.DeleteAll<SpesaTipoPagamento>();

                    App.Database.InsertAll(result.Tipologie);
                    App.Database.InsertAll(result.MetodiPagamento);

                    App.Database.CommitTrans();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                App.Database.RollbackTrans();
                Debug.WriteLine($"Errore Sync Decodifiche: {ex.Message}");
                return false;
            }
        }

        // Metodo per scaricare le note spesa
        public static async Task<bool> GetNoteSpesa(bool aggiorna)
        {
            if (!Settings.IsLogged) return false;

            object credenziali = new { login = Settings.Username, password = Settings.Password };
            DateTime? from = aggiorna ? Settings.LastSyncNoteSpesa : null;

            RestRequest request = CreateBasicAuthRequest(Constants.NOTE_SPESA, Method.Post);
            request.AddBody(new { credenziali, from });

            try
            {
                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content)) return false;

                var result = JsonConvert.DeserializeObject<ServerResponse<int, SpesaDettaglioResponse>>(response.Content);
                if (result == null || result.keys == null || result.values == null) return false;

                App.Database.BeginTrans();

                var speseLocaliEsistenti = App.Database.GetAll<SpesaDettaglio>()
                    .Where(x => x.cod_dip == Settings.CodDipendente)
                    .ToList();

                var idNoteProtette = speseLocaliEsistenti // Note in attesa di essere inviate
                    .Where(x => x.IsLocale)
                    .Select(x => x.id_server)
                    .ToList();

                if (aggiorna)
                {
                    App.Database.Delete<SpesaDettaglio>(x => result.keys.Contains(x.id_server) && !idNoteProtette.Contains(x.id_server));
                }
                else
                {
                    App.Database.Delete<SpesaDettaglio>(x => x.cod_dip == Settings.CodDipendente && !idNoteProtette.Contains(x.id_server));
                }

                var recordDaInserire = result.values
                    .Where(dto => dto.cod_dip == Settings.CodDipendente)
                    .Where(dto => !idNoteProtette.Contains(dto.id))
                    .Select(dto =>
                    {
                        var spesaPrecedente = speseLocaliEsistenti.FirstOrDefault(x => x.id_server == dto.id);

                        return new SpesaDettaglio
                        {
                            id_server = dto.id,
                            cod_dip = dto.cod_dip,
                            cod_cli = dto.cod_cli,
                            da_data = dto.da_data,
                            a_data = dto.a_data,
                            tipologia = dto.tipologia,
                            tipo_tab_tns = dto.tipo_tab_tns,
                            nr_dip_ergon = dto.nr_dip_ergon,
                            flag_con_cli = (dto.flag_con_cli == "S"),
                            foto_scontrino = dto.foto_scontrino,
                            path_scontrino_loc = spesaPrecedente?.path_scontrino_loc,
                            importo = dto.importo,
                            divisa = dto.divisa,
                            flag_tipo_pag = dto.flag_tipo_pag,
                            nr_doc_scontrino = dto.nr_doc_scontrino,
                            data_scontrino = dto.data_scontrino,
                            rag_soc_scontrino = dto.rag_soc_scontrino,
                            partita_iva = dto.partita_iva,
                            note = dto.note,
                            flag_cont = (dto.flag_cont == "S"),
                            IsLocale = false
                        };
                    }).ToList();
                   
                App.Database.InsertAll(recordDaInserire);

                App.Database.CommitTrans();

                Settings.LastSyncNoteSpesa = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                App.Database.RollbackTrans();
                Debug.WriteLine($"Errore Sync Note Spesa: {ex.Message}");
                return false;
            }
        }

        // Metodo per aggiungere una o più note spesa
        public static async Task<bool> SalvaNoteSpesa(List<SpesaDettaglio> noteSpesa)
        {
            try
            {
                if (!Settings.IsLogged) return false;

                object credenziali = new { login = Settings.Username, password = Settings.Password };

                var noteSpesaMappate = new List<object>();
                foreach (var s in noteSpesa)
                {
                    noteSpesaMappate.Add(new
                    {
                        s.cod_dip,
                        s.cod_cli,
                        s.da_data,
                        s.a_data,
                        s.tipologia,
                        s.tipo_tab_tns,
                        s.nr_dip_ergon,
                        flag_con_cli = s.flag_con_cli ? "S" : "N",
                        foto_scontrino = await ImageToBase64Async(s.path_scontrino_loc ?? ""),
                        s.importo,
                        s.divisa,
                        s.flag_tipo_pag,
                        s.nr_doc_scontrino,
                        s.data_scontrino,
                        s.rag_soc_scontrino,
                        s.partita_iva,
                        s.note,
                        flag_cont = "N",
                    });
                }

                RestRequest request = CreateBasicAuthRequest(Constants.NOTE_SPESA_SALVA, Method.Post);
                request.AddBody(new
                {
                    credenziali,
                    note_spesa = noteSpesaMappate
                });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    var erroreDettagliato = response.Content;
                    System.Diagnostics.Debug.WriteLine($"DETTAGLIO ERRORE: {erroreDettagliato}");
                }

                return response.IsSuccessful;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in SalvaNoteSpesa: {ex.Message}");
                return false;
            }
        }

        // Metodo per modificare una nota spesa
        public static async Task<bool> ModificaNotaSpesa(SpesaDettaglio spesa)
        {
            try
            {
                if (!Settings.IsLogged) return false;

                object credenziali = new { login = Settings.Username, password = Settings.Password };

                string? fotoPayload = null;
                string? base64 = await ImageToBase64Async(spesa.path_scontrino_loc ?? "");

                if (!string.IsNullOrWhiteSpace(base64))
                {
                    // Caso A: L'utente ha caricato una nuova foto
                    fotoPayload = base64;
                }
                else if (string.IsNullOrWhiteSpace(spesa.foto_scontrino))
                {
                    // Caso B: L'utente ha rimosso la foto esistente
                    fotoPayload = "CANCELLATA";
                }
                else
                {
                    // Caso C: La foto è rimasta invariata
                    fotoPayload = spesa.foto_scontrino;
                }

                var spesaMappata = new
                {
                    id = spesa.id_server,
                    spesa.cod_dip,
                    spesa.cod_cli,
                    spesa.da_data,
                    spesa.a_data,
                    spesa.tipologia,
                    spesa.tipo_tab_tns,
                    spesa.nr_dip_ergon,
                    flag_con_cli = spesa.flag_con_cli ? "S" : "N",
                    foto_scontrino = fotoPayload,
                    spesa.importo,
                    spesa.divisa,
                    spesa.flag_tipo_pag,
                    spesa.nr_doc_scontrino,
                    spesa.data_scontrino,
                    spesa.rag_soc_scontrino,
                    spesa.partita_iva,
                    spesa.note
                };

                RestRequest request = CreateBasicAuthRequest(Constants.NOTE_SPESA_MODIFICA, Method.Post);

                request.AddBody(new
                {
                    credenziali,
                    note_spesa = new[] { spesaMappata }
                });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    Debug.WriteLine($"ERRORE MODIFICA: {response.Content}");
                }

                return response.IsSuccessful;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in ModificaNotaSpesa: {ex.Message}");
                return false;
            }
        }

        // Metodo per modificare più note spesa dopo una modifica in cui si è premuto "AGGIUNGI UN'ALTRA SPESA"
        public static async Task<bool> ModificaNotaSpesaBatch(List<SpesaDettaglio> noteSpesa)
        {
            try
            {
                if (!Settings.IsLogged) return false;

                object credenziali = new { login = Settings.Username, password = Settings.Password };

                var noteMappate = new List<object>();
                foreach (var spesa in noteSpesa)
                {
                    string? fotoPayload = null;
                    string? base64 = await ImageToBase64Async(spesa.path_scontrino_loc ?? "");

                    if (!string.IsNullOrWhiteSpace(base64))
                    {
                        fotoPayload = base64;
                    }
                    else if (string.IsNullOrWhiteSpace(spesa.foto_scontrino))
                    {
                        fotoPayload = "CANCELLATA";
                    }
                    else
                    {
                        fotoPayload = spesa.foto_scontrino;
                    }
                    noteMappate.Add(new
                    {
                        id = spesa.id_server,
                        spesa.cod_dip,
                        spesa.cod_cli,
                        spesa.da_data,
                        spesa.a_data,
                        spesa.tipologia,
                        spesa.tipo_tab_tns,
                        spesa.nr_dip_ergon,
                        flag_con_cli = spesa.flag_con_cli ? "S" : "N",
                        foto_scontrino = fotoPayload,
                        spesa.importo,
                        spesa.divisa,
                        spesa.flag_tipo_pag,
                        spesa.nr_doc_scontrino,
                        spesa.data_scontrino,
                        spesa.rag_soc_scontrino,
                        spesa.partita_iva,
                        spesa.note
                    });
                }

                RestRequest request = CreateBasicAuthRequest(Constants.NOTE_SPESA_MODIFICA, Method.Post);

                request.AddBody(new
                {
                    credenziali,
                    note_spesa = noteMappate
                });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    Debug.WriteLine($"ERRORE MODIFICA BATCH: {response.Content}");
                }

                return response.IsSuccessful;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in ModificaNotaSpesaBatch: {ex.Message}");
                return false;
            }
        }

        public static async Task<RisultatoAnalisi> AnalizzaScontrinoAsync(string imagePath)
        {
            var risultato = new RisultatoAnalisi();

            try
            {
                if (!Settings.IsLogged)
                {
                    risultato.ErrorMessage = "Utente non autenticato";
                    return risultato;
                }

                string? base64 = await ImageToBase64Async(imagePath);
                if (string.IsNullOrWhiteSpace(base64))
                {
                    risultato.ErrorMessage = "Impossibile elaborare l'immagine della fotocamera.";
                    return risultato;
                }

                object credenziali = new { login = Settings.Username, password = Settings.Password };
                object richiesta_analisi = new { FotoBase64 = base64 };

                RestRequest request = CreateBasicAuthRequest(Constants.NOTE_SPESA_ANALIZZA, Method.Post);
                request.AddBody(new { credenziali, richiesta_analisi });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                // Controllo errori di rete
                if (response.ResponseStatus == ResponseStatus.TimedOut ||
                    (response.ResponseStatus == ResponseStatus.Error && response.StatusCode == 0))
                {
                    risultato.IsNetworkError = true;
                    risultato.ErrorMessage = "Impossibile raggiungere il server.";
                    return risultato;
                }

                // Controllo successo HTTP
                if (response.IsSuccessful)
                {
                    risultato.IsSuccess = true;
                    if (!string.IsNullOrWhiteSpace(response.Content))
                    {
                        risultato.Dati = JsonConvert.DeserializeObject<ScontrinoEstratto>(response.Content);
                    }
                    return risultato;
                }
                else
                {
                    risultato.IsServerError = true;

                    string serverMsg = string.IsNullOrWhiteSpace(response.Content)
                               ? $"Errore server ({(int)response.StatusCode})"
                               : response.Content.Trim('\"');

                    risultato.ErrorMessage = serverMsg;
                    Debug.WriteLine($"Errore analisi scontrino dal server: {response.StatusCode} - {response.Content}");
                    return risultato; 
                }
            }
            catch (TaskCanceledException)
            {
                risultato.IsNetworkError = true;
                risultato.ErrorMessage = "La richiesta è andata in timeout.";
                return risultato;
            }
            catch (HttpRequestException)
            {
                risultato.IsNetworkError = true;
                risultato.ErrorMessage = "Si è verificato un errore di rete.";
                return risultato;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Eccezione generica in AnalizzaScontrinoAsync: {ex.Message}");
                risultato.ErrorMessage = "Errore imprevisto nell'app durante l'elaborazione.";
                return risultato;
            }
        }

        private static async Task<string?> ImageToBase64Async(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] imageBytes = await File.ReadAllBytesAsync(path);
                return Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante la conversione in Base64: {ex.Message}");
                return null;
            }
        }

        public static async Task<PlanningResult> SalvaPlanning(List<Planning> planning)
        {
            try
            {
                if (!Settings.IsLogged) return PlanningResult.Ko;

                object credenziali = new { login = Settings.Username, password = Settings.Password };

                RestRequest request = CreateBasicAuthRequest(Constants.PLANNING_SALVA, Method.Post);
                request.Method = Method.Post;
                request.AddBody(new { planning, credenziali });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    return PlanningResult.Ok;
                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    return PlanningResult.Confict;
                else if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                    return PlanningResult.Gone;
                else if (response.StatusCode == System.Net.HttpStatusCode.ExpectationFailed)
                    return PlanningResult.ExpectationFailed;

                return PlanningResult.Ko;
            }
            catch
            {
                return PlanningResult.Ko;
            }
        }
        public static async Task<PlanningCopyResult> CopiaSpostaPlanning(DateTime from, DateTime to, bool sposta)
        {
            try
            {
                if (!Settings.IsLogged) return PlanningCopyResult.Ko;

                object credenziali = new { login = Settings.Username, password = Settings.Password };

                RestRequest request = CreateBasicAuthRequest(sposta ? Constants.PLANNING_SPOSTA : Constants.PLANNING_COPIA, Method.Post);
                request.Method = Method.Post;

                int cod_dip = Settings.CodDipendente;
                object query_planning = new { cod_dip, from, to };
                request.AddBody(new { credenziali, query_planning });

                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    return PlanningCopyResult.Ok;
                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    return PlanningCopyResult.Confict;
                else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return PlanningCopyResult.NoContent;
                else if (response.StatusCode == System.Net.HttpStatusCode.ExpectationFailed)
                    return PlanningCopyResult.ExpectationFailed;
                else if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    return PlanningCopyResult.MethodNotAllowed;
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return PlanningCopyResult.Forbidden;

                return PlanningCopyResult.Ko;
            }
            catch (Exception ex)
            {
                return PlanningCopyResult.Ko;
            }
        }

        // PRENOTAZIONI RISORSE AZIENDALI
        public static async Task<bool> GetPrenotazioni(bool aggiorna)
        {
            if (!Settings.IsLogged) return false;

            object credenziali = new { login = Settings.Username, password = Settings.Password };
            DateTime? from = aggiorna ? Settings.LastSyncPrenotazioni.ToLocalTime() : null;

            RestRequest request = CreateBasicAuthRequest(Constants.PRENOTAZIONI_RISORSE, Method.Post);
            request.AddBody(new { credenziali, from });

            try
            {
                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content)) return false;

                ServerResponse<int, Prenotazione>? prenotazioni = JsonConvert.DeserializeObject<ServerResponse<int, Prenotazione>>(response.Content);
                if (prenotazioni == null || prenotazioni.keys == null || prenotazioni.values == null) return false;

                // Se aggiorno, elimino solo le prenotazioni che mi ha restituito il BE (quelle che sono cambiate)
                App.Database.BeginTrans();
                if (aggiorna)
                {
                    App.Database.Delete<Prenotazione>(x => prenotazioni.keys.Contains(x.id));
                }
                else
                {
                    // Se non aggiorno, elimino tutte le prenotazioni (perché è la prima volta che le prendo, quindi sono tutte "nuove")
                    App.Database.DeleteAll<Prenotazione>();
                }
                App.Database.InsertAll<Prenotazione>(prenotazioni.values);
                App.Database.CommitTrans();
                
                Settings.LastSyncPrenotazioni = DateTime.Now;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in RestService.GetPrenotazioni: {ex.Message}");
                App.Database.RollbackTrans();
                return false;
            }
        }
        public static async Task<PrenotazioneResult> InserisciPrenotazione(Prenotazione prenotazione)
        {
            if (!Settings.IsLogged) return PrenotazioneResult.Unauthorized;

            object credenziali = new { login = Settings.Username, password = Settings.Password };

            RestRequest request = CreateBasicAuthRequest(Constants.INSERISCI_PRENOTAZIONE, Method.Post);
            request.AddBody(new { credenziali, prenotazione });

            try
            {
                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                // Il BE proverà a inserire la prenotazione nel DB ma prima di farlo farà dei controlli
                // (es. se la risorsa è già prenotata in quel giorno/ora, se l'orario è valido, ecc...)
                // Se tutto è andato bene, restituirà Ok e la prenotazione inserita (con id aggiornato).
                // Se no restituirà degli errori specifici che dobbiamo intercettare per mostrare un messaggio all'utente

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    // Se è andato tutto bene, inserisco nel DB locale la prenotazione restituita dal BE (con id e data ultima modifica aggiornati)
                    var prenotazioneInserita = JsonConvert.DeserializeObject<Prenotazione>(response.Content);
                    if (prenotazioneInserita == null) return PrenotazioneResult.InternalServerError;
                    App.Database.Insert<Prenotazione>(prenotazioneInserita);
                    return PrenotazioneResult.Ok;
                }

                // Siccome la sincronizzazione tra dati locali e server avviene ogni tot min, può capitare che in questo intervallo
                // qualcun'altro inserisca/modifichi una prenotazione che va in conflitto con la prenotazione che sto cercando di inserire
                // In questo caso il BE si accorge della sovrapposizione e restituisce Conflict, però l'utente non vedrebbe ancora i dati 
                // aggiornati se non dopo il prossimo sync, pertanto forziamo un Sync solo delle prenotazioni.
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    bool okSynced = await GetPrenotazioni(true);
                    if (!okSynced) return PrenotazioneResult.Ko;
                    return PrenotazioneResult.Conflict;
                }

                if(response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return PrenotazioneResult.Unauthorized;
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    return PrenotazioneResult.BadRequest;
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return PrenotazioneResult.NotFound;
                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    return PrenotazioneResult.Conflict;
                else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    return PrenotazioneResult.ServiceUnavailable;
                else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    return PrenotazioneResult.InternalServerError;
                else
                    return PrenotazioneResult.Ko;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in RestService.InserisciPrenotazione: {ex.Message}");
                return PrenotazioneResult.Ko;
            }
        }
        public static async Task<PrenotazioneResult> DeletePrenotazione(Prenotazione prenotazione)
        {
            if (!Settings.IsLogged) return PrenotazioneResult.Ko;

            object credenziali = new { login = Settings.Username, password = Settings.Password };

            RestRequest request = CreateBasicAuthRequest(Constants.PRENOTAZIONI_RISORSE, Method.Delete);
            request.AddBody(new { credenziali, prenotazione });

            try
            {
                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                // L'unico motivo per cui una prenotazione non verrebbe eliminata è che è stata modificata o eliminata da qualcun'altro dopo l'ultimo sync,
                // In questo caso il BE si accorge e restituisce Conflict(), però l'utente non vedrebbe ancora i dati aggiornati se non dopo il prossimo sync,
                // pertanto forziamo un Sync solo delle prenotazioni.
                // NB: l'API restituisce anche NotFound o Forbidden, ma nel nostro caso questo non può accadere(a meno di BUG) perchè se si riesce a fare la richiesta
                // vuol dire che la prenotazione prima era presente e modificabile dall'utente, quindi significa che nel frattempo è stata modificata/eliminata
                // da qualcun altro, però questo viene intercettato nel caso di Conflict. Nell'API sono presenti per la sicurezza dell'API, ma nel nostro caso potrebbe 
                // presentarsi solo per bug e quindi per evitarli, eliminiamo anche in questi due casi la Prenotazione

                if (response.IsSuccessful || response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    App.Database.Delete<Prenotazione>(x => x.id == prenotazione.id);
                    return PrenotazioneResult.Ok;
                }

                // Gestiamo in caso di Conflict, forzando la SYNC
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    bool okSynced = await GetPrenotazioni(true);
                    if (!okSynced) return PrenotazioneResult.Ko;
                    return PrenotazioneResult.Conflict;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return PrenotazioneResult.Unauthorized;
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    return PrenotazioneResult.BadRequest;
                else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    return PrenotazioneResult.ServiceUnavailable;
                else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    return PrenotazioneResult.InternalServerError;
                else
                    return PrenotazioneResult.Ko;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in RestService.DeletePrenotazione: {ex.Message}");
                return PrenotazioneResult.Ko;
            }
        }
        public static async Task<PrenotazioneResult> UpdatePrenotazione(Prenotazione prenotazione)
        {
            if (!Settings.IsLogged) return PrenotazioneResult.Ko;

            object credenziali = new { login = Settings.Username, password = Settings.Password };

            RestRequest request = CreateBasicAuthRequest(Constants.PRENOTAZIONI_RISORSE, Method.Put);
            request.AddBody(new { credenziali, prenotazione });

            try
            {
                RestClient client = GetErgClient();
                RestResponse response = await client.ExecuteAsync(request);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    // Se è andato tutto bene, aggiorno nel DB locale la prenotazione aggiornata restituita dal BE che contiene anche
                    // la data di ultima modifica
                    var prenotazioneAggiornata = JsonConvert.DeserializeObject<Prenotazione>(response.Content);
                    if (prenotazioneAggiornata == null) return PrenotazioneResult.InternalServerError;

                    App.Database.BeginTrans();
                    App.Database.Delete<Prenotazione>(x => x.id == prenotazioneAggiornata.id);
                    App.Database.Insert<Prenotazione>(prenotazioneAggiornata);
                    App.Database.CommitTrans();

                    return PrenotazioneResult.Ok;
                }

                /*
                 * Nel caso della modifica abbiamo 2 scenari limite che dobbiamo gestire:
                 * 1. Visto SYNC che viene fatto ogni tot minuti, potrebbe essere che in locale non vedo sovrapposizioni
                 *    ma nel frattempo(nel tempo che passa tra 2 sync) qualcun altro abbia inserito una prenotazione
                 *    con la quale la mia modificata si sovrappone. In questo caso ci verrà resituito un codice di Conflict.
                 *    
                 * 2. Siccome 2 utenti possono modificare la stessa prenotazione(se l'ha fatta uno ed è a nome dell'altro),
                 *    potrebbe succedere che l'altro l'ha modificata prima di me, quindi se io la vado a modificare, la mia
                 *    versione locale non è aggiornata e rischio di sovrascrivere con una versione non coerente, il server
                 *    si accorge di questo caso e ci restituisce sempre conflict.
                 *    
                 * In entrambi i casi forziamo un sync e annulliamo la modifica per mostrare all'utente i dati aggiornati
                 */
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    bool okSynced = await GetPrenotazioni(true);
                    if (!okSynced) return PrenotazioneResult.Ko;
                    return PrenotazioneResult.Conflict;
                }

                // NB: l'API potrebbe restituire anche NotFound(per risorsa o dipendente mancante o prenotazione non trovata) o Forbidden,
                // ma nel nostro caso questo non può accadere(a meno di BUG) perchè se si riesce a fare la richiesta
                // vuol dire che la prenotazione prima era presente e modificabile dall'utente, quindi significa che nel frattempo è stata modificata/eliminata
                // da qualcun altro, però questo viene intercettato nel caso di Conflict. Nell'API sono presenti per la sicurezza dell'API, ma nel nostro caso non servono.
                // In caso di bug eliminiamo il record per evitare errori.
                if(response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    App.Database.Delete<Prenotazione>(x => x.id == prenotazione.id);
                    return PrenotazioneResult.Ok;
                }

                // Gestiamo il resto degli errori
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return PrenotazioneResult.Unauthorized;
                else if(response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    return PrenotazioneResult.BadRequest;
                else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    return PrenotazioneResult.ServiceUnavailable;
                else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    return PrenotazioneResult.InternalServerError;
                else
                    return PrenotazioneResult.Ko;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore in RestService.UpdatePrenotazione: {ex.Message}");
                return PrenotazioneResult.Ko;
            }
        }
    }
}