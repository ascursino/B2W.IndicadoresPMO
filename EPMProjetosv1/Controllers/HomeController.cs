using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Web;
using System.Data;
using System.Data.Services;
using System.Web.Mvc;
using System.Globalization;
using Microsoft.SharePoint;
using Newtonsoft.Json;
using EPMProjetosv1.Models;
using EPMProjetosv1.PSS.Project;
using EPMProjetosv1.PSS.CustomFields;
using EPMProjetosv1.PSS.LockupTable;
using EPMProjetosv1.PSS.QueueSystem;
using EPMProjetosv1.PSS.ListData;

namespace EPMProjetosv1.Controllers
{
    public class HomeController : Controller
    {
        //define web services
        static ProjectSoapClient projectSvc;
        static CustomFieldsSoapClient customfieldSvc;
        static LookupTableSoapClient loockuptableSvc;
        static QueueSystemSoapClient queuesystemSvc;
        

        List<mdProjeto> ListaProjetosResult = new List<mdProjeto>();
        List<mdIndicador> ListaIndicadorResult = new List<mdIndicador>();

        #region Métodos de geração dos indicadores para o KPI

        //via JSON
        public JsonResult GetIndicadorPMOjson()
        {
            GeraProjetosAtivos();
            //GeraProjetosAtivosTESTE();

            //Valida dados dos projetos recuperados. Importante para formatação dos tipos dos campos.
            ValidaListaProjetos();

            //indicador: Projetos Ativos
            GeraIndicador_ProjetosAtivos();

            //indicador: INCG
            GeraIndicador_INCG();

            //indicador: Relação P100D
            GeraIndicador_RelacaoP100D();
            
            return this.Json(ListaIndicadorResult, JsonRequestBehavior.AllowGet);
        }

        //via gravação em lista do Sharepoint
        public void GetIndicadorPMOlista()
        {
            GeraProjetosAtivos();
            //GeraProjetosAtivosTESTE();

            //Valida dados dos projetos recuperados. Importante para formatação dos tipos dos campos.
            ValidaListaProjetos();

            //indicador: Projetos Ativos
            GeraIndicador_ProjetosAtivos();

            //indicador: INCG
            GeraIndicador_INCG(); 

            //indicador: Relação P100D
            GeraIndicador_RelacaoP100D();

            //grava na lista de indicadores do pwa
            GravaListaIndicadores();

            //grava na lista de histórico de indicadores do pwa
            GravaListaHistoricoIndicadores();

            Response.Redirect("/indicadorpmo");
        }

        #endregion


        #region Métodos Auxiliares

        public void GeraProjetosAtivos()
        {
            #region Inicialização

            //inicializando web services
            projectSvc = new ProjectSoapClient();
            customfieldSvc = new CustomFieldsSoapClient();
            loockuptableSvc = new LookupTableSoapClient();
            queuesystemSvc = new QueueSystemSoapClient();

            //Autenticação
            projectSvc.ClientCredentials.Windows.ClientCredential = new NetworkCredential(
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_User,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Password,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Domain);
            projectSvc.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;

            customfieldSvc.ClientCredentials.Windows.ClientCredential = new NetworkCredential(
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_User,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Password,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Domain);
            customfieldSvc.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;

            loockuptableSvc.ClientCredentials.Windows.ClientCredential = new NetworkCredential(
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_User,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Password,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Domain);
            loockuptableSvc.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;

            queuesystemSvc.ClientCredentials.Windows.ClientCredential = new NetworkCredential(
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_User,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Password,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Domain);
            queuesystemSvc.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;

            #endregion

            mdProjeto itemprojetoTemp = null;
            bool vProjValido = false;

            //Lendo todos os projetos
            ProjectDataSet projectList = projectSvc.ReadProjectList();

            foreach (ProjectDataSet.ProjectRow projectRow in projectList.Project)
            {
                //buscando informações de um projeto da lista
                Guid myProjectUid = projectRow.PROJ_UID;

                try
                {
                    ProjectDataSet myProject = projectSvc.ReadProject(myProjectUid, DataStoreEnum.WorkingStore);

                    #region Verificando Projetos Ativos

                    CustomFieldDataSet fCustom = customfieldSvc.ReadCustomFields(string.Empty, false);

                    foreach (ProjectDataSet.ProjectCustomFieldsRow cField in myProject.ProjectCustomFields.Rows)
                    {
                        CustomFieldDataSet.CustomFieldsRow fieldDefinition = fCustom.CustomFields.Single(
                            cfd => cfd.MD_PROP_UID == cField.MD_PROP_UID);

                        if (fieldDefinition.MD_PROP_ID == 190873641) //campo Fase
                        {
                            LookupTableDataSet lookupset = loockuptableSvc.ReadLookupTablesByUids(
                            new Guid[] { fieldDefinition.MD_LOOKUP_TABLE_UID }, false, 1031);
                            LookupTableDataSet.LookupTableTreesRow treeRow = lookupset.LookupTableTrees.FindByLT_STRUCT_UID(cField.CODE_VALUE);

                            //if (treeRow.LT_VALUE_TEXT.ToString() == "Concluído" || treeRow.LT_VALUE_TEXT.ToString() == "Cancelado" || treeRow.LT_VALUE_TEXT.ToString() == "Suspenso") //Fase = Ativo
                            if (treeRow.LT_VALUE_TEXT.ToString().ToUpper() == "INICIAÇÃO" ||
                                treeRow.LT_VALUE_TEXT.ToString().ToUpper() == "PLANEJAMENTO" ||
                                treeRow.LT_VALUE_TEXT.ToString().ToUpper() == "EXECUÇÃO" ||
                                treeRow.LT_VALUE_TEXT.ToString().ToUpper() == "ENCERRAMENTO") //PROJETOS ATIVOS
                            { vProjValido = true; }
                            else
                            { vProjValido = false; }

                            break;
                        }
                    }
                    #endregion

                    if (vProjValido == true)
                    {
                        #region Dados do projeto

                        ProjectDataSet.ProjectRow myProjectItem = myProject.Project[0];

                        itemprojetoTemp = new mdProjeto();

                        itemprojetoTemp.ProjetoId = myProjectItem.PROJ_UID;
                        itemprojetoTemp.Nome = myProjectItem.PROJ_NAME;

                        try { itemprojetoTemp.Inicio = myProjectItem.PROJ_INFO_START_DATE.ToShortDateString(); }
                        catch { itemprojetoTemp.Inicio = string.Empty; }

                        try { itemprojetoTemp.Termino = myProjectItem.PROJ_INFO_FINISH_DATE.ToShortDateString(); }
                        catch { itemprojetoTemp.Termino = string.Empty; }

                        try { itemprojetoTemp.UltimaModificacao = myProjectItem.PROJ_LAST_SAVED.ToShortDateString(); }
                        catch { itemprojetoTemp.UltimaModificacao = string.Empty; }

                        try { itemprojetoTemp.UltimaPublicacao = myProjectItem.WPROJ_LAST_PUB.ToShortDateString(); }
                        catch { itemprojetoTemp.UltimaPublicacao = string.Empty; }

                        try { itemprojetoTemp.Criado = myProjectItem.CREATED_DATE.ToShortDateString(); }
                        catch { itemprojetoTemp.Criado = string.Empty; }

                        #endregion

                        #region Buscando dados customizados do projeto

                        CustomFieldDataSet fieldDefs = customfieldSvc.ReadCustomFields(string.Empty, false);

                        foreach (ProjectDataSet.ProjectCustomFieldsRow cField in myProject.ProjectCustomFields.Rows)
                        {
                            try
                            {
                                CustomFieldDataSet.CustomFieldsRow fieldDefinition = fieldDefs.CustomFields.Single(
                                    cfd => cfd.MD_PROP_UID == cField.MD_PROP_UID);

                                string vCampo = fieldDefinition.MD_PROP_NAME;
                                int vCampoID = fieldDefinition.MD_PROP_ID;
                                string vValor = string.Empty;

                                try
                                {
                                    if (cField.FIELD_TYPE_ENUM == 4) { vValor = cField.DATE_VALUE.ToString(); }
                                    if (cField.FIELD_TYPE_ENUM == 6) { vValor = cField.DUR_VALUE.ToString() + cField.DUR_FMT.ToString(); }
                                    if (cField.FIELD_TYPE_ENUM == 9 || cField.FIELD_TYPE_ENUM == 15) { vValor = cField.NUM_VALUE.ToString(); }
                                    if (cField.FIELD_TYPE_ENUM == 17) { vValor = cField.FLAG_VALUE.ToString(); }

                                    if (cField.FIELD_TYPE_ENUM == 21)
                                    {
                                        try
                                        {
                                            if (DBNull.Value.Equals(cField.TEXT_VALUE) == false)
                                            {
                                                vValor = cField.TEXT_VALUE.ToString();
                                            }
                                            else
                                            {
                                                vValor = string.Empty;
                                            }

                                        }
                                        catch
                                        {
                                            LookupTableDataSet lookupset = loockuptableSvc.ReadLookupTablesByUids(
                                                                        new Guid[] { fieldDefinition.MD_LOOKUP_TABLE_UID }, false, 1031);
                                            LookupTableDataSet.LookupTableTreesRow treeRow = lookupset.LookupTableTrees.FindByLT_STRUCT_UID(cField.CODE_VALUE);

                                            vValor = treeRow.LT_VALUE_TEXT.ToString();
                                        }
                                    }
                                }
                                catch { vValor = string.Empty; }

                                switch (vCampoID)
                                {
                                    case 190873641:
                                        itemprojetoTemp.Fase = vValor;
                                        break;
                                    case 190873654:
                                        itemprojetoTemp.PlanejadoPerc = vValor;
                                        break;
                                    case 190873652:
                                        itemprojetoTemp.DesvioPrazo = vValor;
                                        break;
                                    case 190873676:
                                        itemprojetoTemp.Situacao = vValor;
                                        break;
                                    case 190873649:
                                        itemprojetoTemp.Gerente = vValor;
                                        break;
                                    case 190873678:
                                        itemprojetoTemp.QtdeLinhaBase = vValor;
                                        break;
                                    case 190873660:
                                        itemprojetoTemp.EDTPlano100Dias = vValor;
                                        break;
                                    case 190873661:
                                        itemprojetoTemp.NomePlano100Dias = vValor;
                                        break;
                                    case 190873650:
                                        itemprojetoTemp.Sponsor = vValor;
                                        break;
                                    case 190873662:
                                        itemprojetoTemp.TAP = vValor;
                                        break;
                                    case 190873670:
                                        itemprojetoTemp.AprovacaoTAPEnvio = vValor;
                                        break;
                                    case 190873663:
                                        itemprojetoTemp.AprovacaoTAP = vValor;
                                        break;
                                    case 190873664:
                                        itemprojetoTemp.PGP = vValor;
                                        break;
                                    case 190873669:
                                        itemprojetoTemp.AprovacaoPGPEnvio = vValor;
                                        break;
                                    case 190873665:
                                        itemprojetoTemp.AprovacaoPGP = vValor;
                                        break;
                                    case 190873667:
                                        itemprojetoTemp.TEP = vValor;
                                        break;
                                    case 190873671:
                                        itemprojetoTemp.AprovacaoTEPEnvio = vValor;
                                        break;
                                    case 190873668:
                                        itemprojetoTemp.AprovacaoTEP = vValor;
                                        break;
                                    case 190873666:
                                        itemprojetoTemp.UltimaRSP = vValor;
                                        break;
                                    case 190873648:
                                        itemprojetoTemp.Frente = vValor;
                                        break;
                                    case 190873659:
                                        itemprojetoTemp.Trimestre = vValor;
                                        break;
                                    case 190873642:
                                        itemprojetoTemp.DtInicioPlanejamento = vValor;
                                        break;
                                    case 190873643:
                                        itemprojetoTemp.DtInicioExecucao = vValor;
                                        break;
                                    case 190873644:
                                        itemprojetoTemp.DtInicioEncerramento = vValor;
                                        break;
                                    case 190873646:
                                        itemprojetoTemp.DtTermino = vValor;
                                        break;
                                    case 190873645:
                                        itemprojetoTemp.DtSuspensao = vValor;
                                        break;
                                    case 190873680:
                                        itemprojetoTemp.E100Dias = vValor;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            catch
                            {

                            }
                        }
                        #endregion

                        #region Buscando dados da tarefa de resumo do projeto

                        ProjectDataSet.TaskRow myTaskItem = myProject.Task[0];

                        try { itemprojetoTemp.ConcluidoPerc = myTaskItem.TASK_PCT_COMP.ToString() + "%"; }
                        catch { itemprojetoTemp.ConcluidoPerc = string.Empty; }

                        try { itemprojetoTemp.TerminoLinhaBase = myTaskItem.TB_FINISH.ToShortDateString(); }
                        catch { itemprojetoTemp.TerminoLinhaBase = string.Empty; }

                        try { itemprojetoTemp.Duracao = decimal.Round((decimal.Parse(myTaskItem.TASK_DUR.ToString()) / 10) / decimal.Parse(myProjectItem.PROJ_OPT_MINUTES_PER_DAY.ToString()), 3).ToString() + "d"; }
                        catch { itemprojetoTemp.Duracao = string.Empty; }

                        try { itemprojetoTemp.Trabalho = decimal.Round((decimal.Parse(myTaskItem.TASK_WORK.ToString()) / 1000) / 60, 3).ToString() + "h"; }
                        catch { itemprojetoTemp.Trabalho = string.Empty; }

                        #endregion


                        if (itemprojetoTemp.Fase != null)  //necessário, pois qdo o campo está em branco, sistema não gera o campo.
                        {
                            if (itemprojetoTemp.Gerente != "Andrea Cardozo") //projetos da Andrea Cardozo não contam para os indicadores.
                            {
                                ListaProjetosResult.Add(itemprojetoTemp);
                            }
                        }
                    }
                }
                catch { }
            }
        }
 
        public void GeraProjetosAtivosTESTE()
        {
            mdProjeto itemprojetoTemp = null;

            #region projeto - item 1
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("fc1957c6-58c9-4613-877d-1ab2da237a0b");
            itemprojetoTemp.Fase = "Encerramento";
            itemprojetoTemp.Nome = "14120201 - Submarino Tablet Site";
            itemprojetoTemp.ConcluidoPerc = "88%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "46";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "24/02/2014";
            itemprojetoTemp.Termino = "23/06/2014";
            itemprojetoTemp.Gerente = "Marcos Alvarães";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "02/05/2014";
            itemprojetoTemp.Duracao = "111,5d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = "1.4.1.5";
            itemprojetoTemp.NomePlano100Dias = "Submarino Tablet Site V1";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "17/04/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Front";
            itemprojetoTemp.Trimestre = "2014-01";
            itemprojetoTemp.Criado = "24/02/2014";
            itemprojetoTemp.DtInicioPlanejamento = "24/02/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "02/04/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = "19/05/2014 09:00:00";
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "03/06/2014";
            itemprojetoTemp.UltimaPublicacao = "29/05/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 2
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("31292b44-b63f-4ff3-8bea-3fa8335cde85");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210206 - Reorg HML UOL";
            itemprojetoTemp.ConcluidoPerc = "97%";
            itemprojetoTemp.PlanejadoPerc = "88,000000";
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = "0,000000";
            itemprojetoTemp.Inicio = "10/04/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = "10/07/2014";
            itemprojetoTemp.Duracao = "63d";
            itemprojetoTemp.Trabalho = "1068h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "19/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "19/05/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = "22/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "14/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = "14/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "19/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "26/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 3
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("6e202509-0a73-4cc2-a8ae-00afc09a46ed");
            itemprojetoTemp.Fase = "Iniciação";
            itemprojetoTemp.Nome = "100D-14T1";
            itemprojetoTemp.ConcluidoPerc = "97%";
            itemprojetoTemp.PlanejadoPerc = "0,000000";
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = "3,000000";
            itemprojetoTemp.Inicio = "02/01/2014";
            itemprojetoTemp.Termino = "10/04/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "80d";
            itemprojetoTemp.Trabalho = "43070h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "28/01/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "11/04/2014";
            itemprojetoTemp.UltimaPublicacao = "11/04/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 4
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("12e0f673-8791-44e9-be72-3355be539bf8");
            itemprojetoTemp.Fase = "Iniciação";
            itemprojetoTemp.Nome = "100D-14T2_2";
            itemprojetoTemp.ConcluidoPerc = "39%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "01/04/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "70d";
            itemprojetoTemp.Trabalho = "77784,8h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "14/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "14/05/2014";
            itemprojetoTemp.UltimaPublicacao = "14/05/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 5
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("38a5021c-4b96-4dde-83f6-bd21ae765981");
            itemprojetoTemp.Fase = "Iniciação";
            itemprojetoTemp.Nome = "100D-14T2";
            itemprojetoTemp.ConcluidoPerc = "84%";
            itemprojetoTemp.PlanejadoPerc = "0,000000";
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = "3,000000";
            itemprojetoTemp.Inicio = "01/04/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "70d";
            itemprojetoTemp.Trabalho = "78615,6h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "14/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "25/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 6
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("75e48384-af21-4d11-aaa2-0ad996c7f40b");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14230901 - Expansão Finance";
            itemprojetoTemp.ConcluidoPerc = "36%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "15/04/2014";
            itemprojetoTemp.Termino = "29/07/2014";
            itemprojetoTemp.Gerente = "Paulo Pessoa";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "63,875d";
            itemprojetoTemp.Trabalho = "448,00h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = "Expansão Finance";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Pool";
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "07/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "17/06/2014";
            itemprojetoTemp.UltimaPublicacao = "07/05/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 7
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("dc954369-ae23-49d8-8303-0bc588b0468c");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210208 - Ativação da nova infraestrutura de backend (Exadata)";
            itemprojetoTemp.ConcluidoPerc = "84%";
            itemprojetoTemp.PlanejadoPerc = "79,000000";
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = "0,000000";
            itemprojetoTemp.Inicio = "22/04/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "1,000000";
            itemprojetoTemp.TerminoLinhaBase = "10/07/2014";
            itemprojetoTemp.Duracao = "57d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "29/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "02/06/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = "20/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "22/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = "08/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "02/06/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 8
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("c60d5f3b-b2ba-478e-a2cb-1eae39756813");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14130103 - Vale Cultura Ticket";
            itemprojetoTemp.ConcluidoPerc = "34%";
            itemprojetoTemp.PlanejadoPerc = "44,000000";
            itemprojetoTemp.DesvioPrazo = "27";
            itemprojetoTemp.Situacao = "2,000000";
            itemprojetoTemp.Inicio = "19/02/2014";
            itemprojetoTemp.Termino = "13/10/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = "08/09/2014";
            itemprojetoTemp.Duracao = "117d";
            itemprojetoTemp.Trabalho = "1376h";
            itemprojetoTemp.EDTPlano100Dias = "1.8.1.3.2";
            itemprojetoTemp.NomePlano100Dias = "Vale Cultura Ticket - FASE I - Escopo";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "23/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "11/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "19/02/2014";
            itemprojetoTemp.DtInicioPlanejamento = "19/02/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "23/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 9
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("a8f8147e-0da3-4d25-b3e9-2f3266022536");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210207 - Contingência de ambientes em Tamboré";
            itemprojetoTemp.ConcluidoPerc = "91%";
            itemprojetoTemp.PlanejadoPerc = "99,000000";
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = "0,000000";
            itemprojetoTemp.Inicio = "22/04/2014";
            itemprojetoTemp.Termino = "11/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "1,000000";
            itemprojetoTemp.TerminoLinhaBase = "11/07/2014";
            itemprojetoTemp.Duracao = "57,25d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "15/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "15/05/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = "20/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "14/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = "15/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "26/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 10
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("7c33e159-1bd1-4b0a-bbb0-40ecbfd15b67");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210106 - QSM Logistica - Fase 2";
            itemprojetoTemp.ConcluidoPerc = "98%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "02/05/2014";
            itemprojetoTemp.Termino = "02/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "02/07/2014";
            itemprojetoTemp.Duracao = "44d";
            itemprojetoTemp.Trabalho = "1432h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "05/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 11
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("54750461-1892-4b00-958c-5a4daf35a31f");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14230110 - Sistema de Malha";
            itemprojetoTemp.ConcluidoPerc = "0%";
            itemprojetoTemp.PlanejadoPerc = "100,000000";
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = "3,000000";
            itemprojetoTemp.Inicio = "12/05/2014";
            itemprojetoTemp.Termino = "30/01/2015";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "189d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "12/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = "12/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "26/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 12
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("b1c3bfef-cce9-48c1-abc1-5c6e1be47ae5");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14230108 - Roubo e Furto";
            itemprojetoTemp.ConcluidoPerc = "4%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "29/04/2014";
            itemprojetoTemp.Termino = "30/05/2014";
            itemprojetoTemp.Gerente = "Marcos Alvarães";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "23d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Pool";
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "29/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = "29/04/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "17/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 13
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("5d5f9c38-6219-43fd-952f-60cfe8d53088");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14130301 - Phoenix";
            itemprojetoTemp.ConcluidoPerc = "0%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "13/03/2014";
            itemprojetoTemp.Termino = "12/05/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "40d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = "Entendimento do escopo do projeto Phoenix";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = "2014-01";
            itemprojetoTemp.Criado = "19/03/2014";
            itemprojetoTemp.DtInicioPlanejamento = "13/03/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "03/06/2014";
            itemprojetoTemp.UltimaPublicacao = "19/03/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 14
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("1674bb00-7e90-438c-a473-618fb29bd11b");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14130201 - 2014 - Comitê de Mudança";
            itemprojetoTemp.ConcluidoPerc = "58%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "2";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "14/02/2014";
            itemprojetoTemp.Termino = "27/11/2014";
            itemprojetoTemp.Gerente = "Fabiola Ferreira";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "20/11/2014";
            itemprojetoTemp.Duracao = "211d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = "2014 - Comitê de Mudança ";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = "07/03/2014 09:00:00";
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = "18/03/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Pool";
            itemprojetoTemp.Trimestre = "2014-01";
            itemprojetoTemp.Criado = "21/02/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = "20/02/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "12/06/2014";
            itemprojetoTemp.UltimaPublicacao = "10/06/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 15
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("dcbfa17b-cd45-43ec-bf0c-61ef21f643aa");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210205 - Plano GoldenGate para os bancos estratégicos";
            itemprojetoTemp.ConcluidoPerc = "59%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "14/05/2014";
            itemprojetoTemp.Termino = "08/07/2014";
            itemprojetoTemp.Gerente = "Felipe Gandra";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "08/07/2014";
            itemprojetoTemp.Duracao = "40d";
            itemprojetoTemp.Trabalho = "104h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Infra";
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "14/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = "14/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "20/06/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "23/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 16
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("70f8a109-0312-49e1-bed9-6c0196e6f5cb");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14230109 - Plataforma de suporte a seguros";
            itemprojetoTemp.ConcluidoPerc = "0%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "06/05/2014";
            itemprojetoTemp.Termino = "30/06/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "40d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "06/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "06/05/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 17
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("8b2c6888-5435-486c-b398-7c909341eb63");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14210107 - Permissões NETAPP";
            itemprojetoTemp.ConcluidoPerc = "13%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "05/05/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "49d";
            itemprojetoTemp.Trabalho = "276h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = "2014-02";
            itemprojetoTemp.Criado = "05/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "29/05/2014";
            itemprojetoTemp.UltimaPublicacao = "28/05/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 18
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("b7b346ea-1e8f-4b1b-9f52-83227b0ca748");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14230107 - Migração e Integração Ingresso_com";
            itemprojetoTemp.ConcluidoPerc = "73%";
            itemprojetoTemp.PlanejadoPerc = "86,000000";
            itemprojetoTemp.DesvioPrazo = "40";
            itemprojetoTemp.Situacao = "2,000000";
            itemprojetoTemp.Inicio = "29/04/2014";
            itemprojetoTemp.Termino = "08/08/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = "10/07/2014";
            itemprojetoTemp.Duracao = "73d";
            itemprojetoTemp.Trabalho = "1920h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "05/06/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "05/06/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = "17/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "29/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = "29/04/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "05/06/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "26/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 19
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("62147aee-4e62-45df-a63e-83cc64d0c98a");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14130401 - Plataforma Listas";
            itemprojetoTemp.ConcluidoPerc = "50%";
            itemprojetoTemp.PlanejadoPerc = "31,000000";
            itemprojetoTemp.DesvioPrazo = "-1";
            itemprojetoTemp.Situacao = "1,000000";
            itemprojetoTemp.Inicio = "04/04/2014";
            itemprojetoTemp.Termino = "07/08/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "2,000000";
            itemprojetoTemp.TerminoLinhaBase = "08/08/2014";
            itemprojetoTemp.Duracao = "87d";
            itemprojetoTemp.Trabalho = "264h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = "04/04/2014 09:00:00";
            itemprojetoTemp.AprovacaoTAP = "11/04/2014 09:00:00";
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "05/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "05/05/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "27/03/2014";
            itemprojetoTemp.DtInicioPlanejamento = "07/04/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "09/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "05/06/2014";
            itemprojetoTemp.UltimaPublicacao = "05/06/2014";
            itemprojetoTemp.E100Dias = "False";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 20
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("e79af7bd-afc9-483b-ae3c-899c6137c9e3");
            itemprojetoTemp.Fase = "Encerramento";
            itemprojetoTemp.Nome = "130103_38 - Submarino Viagens - Ported Site";
            itemprojetoTemp.ConcluidoPerc = "86%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "132";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "06/11/2013";
            itemprojetoTemp.Termino = "20/06/2014";
            itemprojetoTemp.Gerente = "Marcos Alvarães";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "18/03/2014";
            itemprojetoTemp.Duracao = "114,5d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = "1.5.1.4";
            itemprojetoTemp.NomePlano100Dias = "Mobile Site - desenho dos sites B2W Viagens";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "16/04/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = "2013-04";
            itemprojetoTemp.Criado = "12/11/2013";
            itemprojetoTemp.DtInicioPlanejamento = "06/11/2013 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "02/04/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = "19/05/2014 09:00:00";
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "03/06/2014";
            itemprojetoTemp.UltimaPublicacao = "13/05/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 21
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("1449be2f-9124-4def-b584-9d9c42828f50");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14120301 - Compra Recorrente";
            itemprojetoTemp.ConcluidoPerc = "23%";
            itemprojetoTemp.PlanejadoPerc = "100,000000";
            itemprojetoTemp.DesvioPrazo = "7";
            itemprojetoTemp.Situacao = "2,000000";
            itemprojetoTemp.Inicio = "07/04/2014";
            itemprojetoTemp.Termino = "22/12/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "1,000000";
            itemprojetoTemp.TerminoLinhaBase = "02/12/2014";
            itemprojetoTemp.Duracao = "206d";
            itemprojetoTemp.Trabalho = "232h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "16/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "16/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "07/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = "14/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "26/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 22
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("98c809c4-e7ee-434c-98d0-9eedff846148");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14120104 - Make na Offer SUBA";
            itemprojetoTemp.ConcluidoPerc = "70%";
            itemprojetoTemp.PlanejadoPerc = "74,000000";
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = "0,000000";
            itemprojetoTemp.Inicio = "20/05/2014";
            itemprojetoTemp.Termino = "07/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = "07/07/2014";
            itemprojetoTemp.Duracao = "57,25d";
            itemprojetoTemp.Trabalho = "906h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "10/06/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "21/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = "20/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "10/06/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "13/06/2014";
            itemprojetoTemp.UltimaPublicacao = "13/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 23
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("0c1b4ead-853c-4f3b-998f-c062e909bb8c");
            itemprojetoTemp.Fase = "Suspenso";
            itemprojetoTemp.Nome = "14230801 - Rio 2016";
            itemprojetoTemp.ConcluidoPerc = "0%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "25/04/2014";
            itemprojetoTemp.Termino = "20/06/2014";
            itemprojetoTemp.Gerente = "Gustavo Farias";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "40d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Pool";
            itemprojetoTemp.Trimestre = "2014-02";
            itemprojetoTemp.Criado = "25/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = "24/06/2014 09:00:00";
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "28/04/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 24
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("f5738ca8-5102-42ce-b54b-c55de70072a7");
            itemprojetoTemp.Fase = "Iniciação";
            itemprojetoTemp.Nome = "100D-14T2__280414";
            itemprojetoTemp.ConcluidoPerc = "14%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "01/04/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "70d";
            itemprojetoTemp.Trabalho = "80348,4h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "28/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "29/04/2014";
            itemprojetoTemp.UltimaPublicacao = string.Empty;
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 25
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("85b394a1-47e3-4f05-b6c7-cb1844176aba");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210209 - Processo Contingências - 5W1H";
            itemprojetoTemp.ConcluidoPerc = "61%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "-7";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "12/05/2014";
            itemprojetoTemp.Termino = "07/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "11/07/2014";
            itemprojetoTemp.Duracao = "41d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "06/06/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "10/06/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = "2014-02";
            itemprojetoTemp.Criado = "22/05/2014";
            itemprojetoTemp.DtInicioPlanejamento = "22/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 26
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("550589d5-1823-41f9-8b40-d81678976a9e");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "130106_20 - NF-e - Terceira Geração";
            itemprojetoTemp.ConcluidoPerc = "53%";
            itemprojetoTemp.PlanejadoPerc = "68,000000";
            itemprojetoTemp.DesvioPrazo = "122";
            itemprojetoTemp.Situacao = "2,000000";
            itemprojetoTemp.Inicio = "28/11/2013";
            itemprojetoTemp.Termino = "08/08/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "1,000000";
            itemprojetoTemp.TerminoLinhaBase = "23/07/2014";
            itemprojetoTemp.Duracao = "175d";
            itemprojetoTemp.Trabalho = "641,46h";
            itemprojetoTemp.EDTPlano100Dias = "1.9.2.4";
            itemprojetoTemp.NomePlano100Dias = "NF-e 3.0 - Fase 1 :: Definição";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "16/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "10/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "28/11/2013";
            itemprojetoTemp.DtInicioPlanejamento = "28/11/2013 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "16/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 27
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("9e912782-0816-4e7a-a72b-e20eac2e2437");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14210210 - Separação dos bancos de Vendas - Shop";
            itemprojetoTemp.ConcluidoPerc = "77%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "30/05/2014";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = "Ricardo Rocha";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "10/07/2014";
            itemprojetoTemp.Duracao = "29,25d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Infra";
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "03/06/2014";
            itemprojetoTemp.DtInicioPlanejamento = "30/06/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 28
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("69003b6c-954f-4138-8eeb-edad7fe647c6");
            itemprojetoTemp.Fase = "Encerramento";
            itemprojetoTemp.Nome = "14130104 - Implantação da Solução _12 _24";
            itemprojetoTemp.ConcluidoPerc = "92%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "-3";
            itemprojetoTemp.Situacao = "0,000000";
            itemprojetoTemp.Inicio = "19/02/2014";
            itemprojetoTemp.Termino = "13/06/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "1,000000";
            itemprojetoTemp.TerminoLinhaBase = "17/06/2014";
            itemprojetoTemp.Duracao = "84,25d";
            itemprojetoTemp.Trabalho = "2113,67h";
            itemprojetoTemp.EDTPlano100Dias = "1.8.4.1";
            itemprojetoTemp.NomePlano100Dias = "Implantação da Solução +12 + 24";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "28/03/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "14/04/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = "26/05/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "19/02/2014";
            itemprojetoTemp.DtInicioPlanejamento = "14/02/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "28/03/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = "26/05/2014 09:00:00";
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "05/06/2014";
            itemprojetoTemp.UltimaPublicacao = "04/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 29
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("b9585e68-a1d1-4b2c-bf45-f96075ec46f8");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14130601 - Automação Relatorios DIG";
            itemprojetoTemp.ConcluidoPerc = "80%";
            itemprojetoTemp.PlanejadoPerc = "85,000000";
            itemprojetoTemp.DesvioPrazo = "18";
            itemprojetoTemp.Situacao = "1,000000";
            itemprojetoTemp.Inicio = "29/04/2014";
            itemprojetoTemp.Termino = "23/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "0,000000";
            itemprojetoTemp.TerminoLinhaBase = "10/07/2014";
            itemprojetoTemp.Duracao = "61d";
            itemprojetoTemp.Trabalho = "1472,8h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "12/05/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "12/05/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = "23/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "07/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = "07/04/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = "14/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 30
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("9fbef337-398e-4542-96e9-fd850e79843c");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "14230106 - Pre-autorizacao de cartão de credito";
            itemprojetoTemp.ConcluidoPerc = "57%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "25/04/2014";
            itemprojetoTemp.Termino = "11/07/2014";
            itemprojetoTemp.Gerente = "Gustavo Farias";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "54d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "25/04/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "23/06/2014";
            itemprojetoTemp.UltimaPublicacao = "13/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 31
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("3553c0b3-6e28-4cae-9548-0868649614e5");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "130000_26 - Implantação Novo CD Recife 2014 - Fase 2";
            itemprojetoTemp.ConcluidoPerc = "75%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "11/11/2013";
            itemprojetoTemp.Termino = "07/08/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "07/08/2014";
            itemprojetoTemp.Duracao = "73d";
            itemprojetoTemp.Trabalho = "0h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "02/04/2014 09:00:00";
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "12/11/2013";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = "03/05/2014 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "23/06/2014";
            itemprojetoTemp.UltimaPublicacao = "23/06/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 32
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("d7dc49e7-a405-4922-9722-ece5d3867301");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "130104_1 - XD - Multi CD";
            itemprojetoTemp.ConcluidoPerc = "98%";
            itemprojetoTemp.PlanejadoPerc = "100,000000";
            itemprojetoTemp.DesvioPrazo = "275";
            itemprojetoTemp.Situacao = "2,000000";
            itemprojetoTemp.Inicio = "22/05/2013";
            itemprojetoTemp.Termino = "09/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "1,000000";
            itemprojetoTemp.TerminoLinhaBase = "09/09/2013";
            itemprojetoTemp.Duracao = "287,375d";
            itemprojetoTemp.Trabalho = "1050,4h";
            itemprojetoTemp.EDTPlano100Dias = "1.6.5.5";
            itemprojetoTemp.NomePlano100Dias = "Implantar XD multi-CDs :: Fase II - Teste";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = "12/06/2013 09:00:00";
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = "02/07/2013 09:00:00";
            itemprojetoTemp.UltimaRSP = "28/05/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "28/05/2013";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "26/06/2014";
            itemprojetoTemp.UltimaPublicacao = "26/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 33
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("e8643326-0ecf-4204-b6b8-968acba06249");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "130000_18 - Projeto Internalização ClickRodo";
            itemprojetoTemp.ConcluidoPerc = "62%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "95";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "13/06/2013";
            itemprojetoTemp.Termino = "08/09/2014";
            itemprojetoTemp.Gerente = "Rafael Macedo";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "31/01/2014";
            itemprojetoTemp.Duracao = "313,5d";
            itemprojetoTemp.Trabalho = "3556h";
            itemprojetoTemp.EDTPlano100Dias = "1.7.2.2.7";
            itemprojetoTemp.NomePlano100Dias = "Clickrodo - Integração dos serviços corporativos";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = "06/11/2013 09:00:00";
            itemprojetoTemp.AprovacaoTAP = "17/12/2013 09:00:00";
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "06/11/2013 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "17/12/2013 09:00:00";
            itemprojetoTemp.UltimaRSP = "03/02/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Infra";
            itemprojetoTemp.Trimestre = "2013-03";
            itemprojetoTemp.Criado = "02/07/2013";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = "28/03/2014 09:00:00";
            itemprojetoTemp.UltimaModificacao = "09/06/2014";
            itemprojetoTemp.UltimaPublicacao = "11/05/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 34
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("20e738e1-4863-48c2-bfa2-8c8ee5fa8e38");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "130106_3 - Boleto Lasa";
            itemprojetoTemp.ConcluidoPerc = "94%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "96";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "08/04/2013";
            itemprojetoTemp.Termino = "16/05/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "10/10/2013";
            itemprojetoTemp.Duracao = "310d";
            itemprojetoTemp.Trabalho = "3195,40h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "27/09/2013 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = "Pool";
            itemprojetoTemp.Trimestre = "2013-03";
            itemprojetoTemp.Criado = "08/04/2013";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "03/06/2014";
            itemprojetoTemp.UltimaPublicacao = "29/04/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 35
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("a21d6e15-41a7-47a9-aca2-c1561c020e7d");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "130026 - Plataforma Vale Presente";
            itemprojetoTemp.ConcluidoPerc = "90%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "17/07/2013";
            itemprojetoTemp.Termino = "10/07/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "10/07/2014";
            itemprojetoTemp.Duracao = "248d";
            itemprojetoTemp.Trabalho = "17691,00h";
            itemprojetoTemp.EDTPlano100Dias = "1.2.5.3";
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = "30/07/2013 09:00:00";
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = "05/12/2013 09:00:00";
            itemprojetoTemp.AprovacaoPGP = "09/06/2014 09:00:00";
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = "2013-04";
            itemprojetoTemp.Criado = "30/07/2013";
            itemprojetoTemp.DtInicioPlanejamento = "16/05/2014 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "24/06/2014";
            itemprojetoTemp.UltimaPublicacao = "24/06/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 36
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("64bab019-3e3e-4b77-bc15-be45e6749e9d");
            itemprojetoTemp.Fase = "Execução";
            itemprojetoTemp.Nome = "14130501 - Marketplace - Fase II";
            itemprojetoTemp.ConcluidoPerc = "67%";
            itemprojetoTemp.PlanejadoPerc = "85,000000";
            itemprojetoTemp.DesvioPrazo = "0";
            itemprojetoTemp.Situacao = "1,000000";
            itemprojetoTemp.Inicio = "31/03/2014";
            itemprojetoTemp.Termino = "12/11/2014";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = "5,000000";
            itemprojetoTemp.TerminoLinhaBase = "12/11/2014";
            itemprojetoTemp.Duracao = "159d";
            itemprojetoTemp.Trabalho = "17609,60h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = string.Empty;
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = "NÃO OBRIGATÓRIO";
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = "20/06/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = string.Empty;
            itemprojetoTemp.Criado = "31/03/2014";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "23/06/2014";
            itemprojetoTemp.UltimaPublicacao = "20/06/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 37
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("9bd902a0-eec3-4bfc-a792-53cb95ee5f17");
            itemprojetoTemp.Fase = "Encerramento";
            itemprojetoTemp.Nome = "130106_18_Projeto Minha casa melhor bandeirado";
            itemprojetoTemp.ConcluidoPerc = "99%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = "78";
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "08/10/2013";
            itemprojetoTemp.Termino = "02/04/2014";
            itemprojetoTemp.Gerente = "Clovis Costa";
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = "15/01/2014";
            itemprojetoTemp.Duracao = "121d";
            itemprojetoTemp.Trabalho = "1126,80h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = "Minha Casa Melhor bandeirado - Acom";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = "18/11/2013 09:00:00";
            itemprojetoTemp.UltimaRSP = "28/03/2014 09:00:00";
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = "02/04/2014 09:00:00";
            itemprojetoTemp.Frente = "Front";
            itemprojetoTemp.Trimestre = "2013-04";
            itemprojetoTemp.Criado = "08/10/2013";
            itemprojetoTemp.DtInicioPlanejamento = string.Empty;
            itemprojetoTemp.DtInicioExecucao = "18/11/2013 09:00:00";
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "27/05/2014";
            itemprojetoTemp.UltimaPublicacao = "31/03/2014";
            itemprojetoTemp.E100Dias = "True";

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

            #region projeto - item 38
            itemprojetoTemp = new mdProjeto();
            itemprojetoTemp.ProjetoId = new Guid("71a9bdb2-3758-477b-99f8-634a796e3cd5");
            itemprojetoTemp.Fase = "Planejamento";
            itemprojetoTemp.Nome = "130106_13 - ICMS-ST";
            itemprojetoTemp.ConcluidoPerc = "30%";
            itemprojetoTemp.PlanejadoPerc = string.Empty;
            itemprojetoTemp.DesvioPrazo = string.Empty;
            itemprojetoTemp.Situacao = string.Empty;
            itemprojetoTemp.Inicio = "05/08/2013";
            itemprojetoTemp.Termino = "27/11/2013";
            itemprojetoTemp.Gerente = string.Empty;
            itemprojetoTemp.QtdeLinhaBase = string.Empty;
            itemprojetoTemp.TerminoLinhaBase = string.Empty;
            itemprojetoTemp.Duracao = "81d";
            itemprojetoTemp.Trabalho = "482h";
            itemprojetoTemp.EDTPlano100Dias = string.Empty;
            itemprojetoTemp.NomePlano100Dias = "Correção do sistema para destacar o ICMS-ST na venda quando operação Interestadual para PJ (caso AMBEV)";
            itemprojetoTemp.Sponsor = string.Empty;
            itemprojetoTemp.TAP = string.Empty;
            itemprojetoTemp.AprovacaoTAPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTAP = string.Empty;
            itemprojetoTemp.PGP = string.Empty;
            itemprojetoTemp.AprovacaoPGPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoPGP = string.Empty;
            itemprojetoTemp.UltimaRSP = string.Empty;
            itemprojetoTemp.TEP = string.Empty;
            itemprojetoTemp.AprovacaoTEPEnvio = string.Empty;
            itemprojetoTemp.AprovacaoTEP = string.Empty;
            itemprojetoTemp.Frente = string.Empty;
            itemprojetoTemp.Trimestre = "2013-03";
            itemprojetoTemp.Criado = "05/08/2013";
            itemprojetoTemp.DtInicioPlanejamento = "12/12/2013 09:00:00";
            itemprojetoTemp.DtInicioExecucao = string.Empty;
            itemprojetoTemp.DtInicioEncerramento = string.Empty;
            itemprojetoTemp.DtSuspensao = string.Empty;
            itemprojetoTemp.UltimaModificacao = "03/06/2014";
            itemprojetoTemp.UltimaPublicacao = "25/03/2014";
            itemprojetoTemp.E100Dias = string.Empty;

            ListaProjetosResult.Add(itemprojetoTemp);
            #endregion

        }

        public void GeraIndicador_ProjetosAtivos()
        {
            mdIndicador itemIndicador = new mdIndicador();

            itemIndicador.NomeIndicador = "ProjetosAtivos";
            itemIndicador.Titulo = "Projetos Ativos";
            itemIndicador.Valor = ListaProjetosResult.Count().ToString();
            itemIndicador.Status = "UNDEFINED";
            itemIndicador.Data = DateTime.Today.ToShortDateString();

            ListaIndicadorResult.Add(itemIndicador);
        }

        public void GeraIndicador_INCG()
        {
            decimal ValorINCG = 0;
            decimal ValorInic = 0; decimal TotProjInicTap = 0; decimal TotProjInic = 0;
            decimal ValorPlanej = 0; decimal TotProjPlanTap = 0; decimal TotProjPlan = 0;
            decimal ValorExecT = 0; decimal TotProjExecTap = 0; decimal TotProjExec = 0;
            decimal ValorExecP = 0; decimal TotProjExecPGP = 0;
            decimal ValorExecRSP = 0; decimal TotProjExecRSP = 0; decimal TotProjExecRSPData = 0;
            decimal ValorExecCron = 0; decimal TotProjExecPub = 0; decimal TotProjEncePub = 0; decimal TotProjEnce = 0;
            string vStatus = string.Empty;
            DateTime DataBase = DateTime.Today;
            //DateTime DataBase = new DateTime(2014,08,06);

            foreach(mdProjeto projetoitem in ListaProjetosResult)
            {
                //ValorInic
                if (projetoitem.Fase.ToUpper() == "INICIAÇÃO" && projetoitem.TAP == string.Empty)
                { TotProjInicTap +=1; }

                if (projetoitem.Fase.ToUpper() == "INICIAÇÃO")
                { TotProjInic +=1; }

                //ValorPlanej
                if (projetoitem.Fase.ToUpper() == "PLANEJAMENTO" && projetoitem.TAP == string.Empty)
                { TotProjPlanTap +=1; }

                if (projetoitem.Fase.ToUpper() == "PLANEJAMENTO")
                { TotProjPlan +=1; }

                //ValorExecT
                if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && projetoitem.TAP == string.Empty)
                { TotProjExecTap +=1; }

                if (projetoitem.Fase.ToUpper() == "EXECUÇÃO")
                { TotProjExec +=1; }

                //ValorExecP
                //if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && projetoitem.PGP.ToUpper() == "SIM")
                if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && projetoitem.PGP == string.Empty)
                { TotProjExecPGP +=1; }

                //ValorExecRSP
                if (projetoitem.UltimaRSP != string.Empty)
                {
                    //if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && DateTime.Parse(projetoitem.UltimaRSP) == DataBase.Subtract(TimeSpan.FromDays(7)))
                    if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && DateTime.Parse(projetoitem.UltimaRSP) < DataBase.Subtract(TimeSpan.FromDays(7)))
                    { TotProjExecRSPData += 1; }
                }

                //if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && projetoitem.UltimaRSP == string.Empty)
                if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && projetoitem.UltimaRSP == string.Empty && DateTime.Parse(projetoitem.DtInicioExecucao) < DataBase.Subtract(TimeSpan.FromDays(7)))
                { TotProjExecRSP +=1; }

                //ValorExecCron
                if (projetoitem.UltimaPublicacao != string.Empty)
                {
                    //if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && DateTime.Parse(projetoitem.UltimaPublicacao) == DataBase.Subtract(TimeSpan.FromDays(15)))
                    if (projetoitem.Fase.ToUpper() == "EXECUÇÃO" && DateTime.Parse(projetoitem.UltimaPublicacao) < DataBase.Subtract(TimeSpan.FromDays(15)))
                    { TotProjExecPub += 1; }

                    //if (projetoitem.Fase.ToUpper() == "ENCERRAMENTO" && DateTime.Parse(projetoitem.UltimaPublicacao) == DataBase.Subtract(TimeSpan.FromDays(15)))
                    if (projetoitem.Fase.ToUpper() == "ENCERRAMENTO" && DateTime.Parse(projetoitem.UltimaPublicacao) < DataBase.Subtract(TimeSpan.FromDays(15)))
                    { TotProjEncePub += 1; }
                }
                
                if (projetoitem.Fase.ToUpper() == "ENCERRAMENTO")
                { TotProjEnce +=1; }
            }

            if (TotProjInic != 0)
            {
                ValorInic = TotProjInicTap / TotProjInic;
            }

            if (TotProjPlan != 0)
            {
                ValorPlanej = TotProjPlanTap / TotProjPlan;
            }

            if (TotProjExec != 0)
            {
                ValorExecT = TotProjExecTap / TotProjExec;
                ValorExecP = TotProjExecPGP / TotProjExec;
                ValorExecRSP = (TotProjExecRSPData + TotProjExecRSP) / TotProjExec;
            }

            if (TotProjExec != 0 || TotProjEnce != 0)
            {
                ValorExecCron = (TotProjExecPub + TotProjEncePub) / (TotProjExec + TotProjEnce);
            }

            ValorINCG = decimal.Round(ValorInic + ValorPlanej + ValorExecT + ValorExecP + ValorExecRSP + ValorExecCron, 2);

            //verificando status do indicador
            if (ValorINCG >= 0 && ValorINCG <= decimal.Parse("0,60"))
            {
                vStatus = "CLEAR";
            }
            else
            {
                if (ValorINCG >= decimal.Parse("0,61") && ValorINCG <= 1)
                {
                    vStatus = "WARN";
                }
                else
                {
                    if(ValorINCG > 1)
                    {
                        vStatus = "CRITICAL";
                    }
                }
            }

            mdIndicador itemIndicador = new mdIndicador();

            itemIndicador.NomeIndicador = "INCG";
            itemIndicador.Titulo = "INCG";
            itemIndicador.Valor = ValorINCG.ToString();
            itemIndicador.Status = vStatus;
            itemIndicador.Data = DataBase.ToShortDateString();

            ListaIndicadorResult.Add(itemIndicador);
        }

        public void GeraIndicador_RelacaoP100D()
        {
            decimal ValorRelacaoP100D = 0;
            decimal TotProj100D = 0;
            decimal TotProjAtivos = 0;
            string vStatus = string.Empty;
            DateTime DataBase = DateTime.Today;
            //DateTime DataBase = new DateTime(2014,07,29);

            foreach(mdProjeto projetoitem in ListaProjetosResult)
            {
                //TotProj100D
                if (projetoitem.E100Dias.ToUpper() == "TRUE")
                { TotProj100D +=1; }
            }

            //TotProjAtivos
            TotProjAtivos = ListaProjetosResult.Count;

            if (TotProjAtivos != 0)
            {
                ValorRelacaoP100D = decimal.Round((TotProj100D / TotProjAtivos)*100, 2);
            }

            //verificando status do indicador
            if (ValorRelacaoP100D >= decimal.Parse("0,80"))
            {
                vStatus = "CLEAR";
            }
            else
            {
                vStatus = "CRITICAL";
            }

            mdIndicador itemIndicador = new mdIndicador();

            itemIndicador.NomeIndicador = "RelacaoP100D";
            itemIndicador.Titulo = "Relação P100D";
            //itemIndicador.Valor = string.Format("{0:P}", ValorRelacaoP100D).ToString();
            itemIndicador.Valor = ValorRelacaoP100D + "%";
            itemIndicador.Status = vStatus;
            itemIndicador.Data = DataBase.ToShortDateString();

            ListaIndicadorResult.Add(itemIndicador);
       }

        public void ValidaListaProjetos()
        {
            foreach (mdProjeto itemprojeto in ListaProjetosResult)
            {
                if (itemprojeto.AprovacaoPGP == null) { itemprojeto.AprovacaoPGP = string.Empty; }
                if (itemprojeto.AprovacaoPGPEnvio == null) { itemprojeto.AprovacaoPGPEnvio = string.Empty; }
                if (itemprojeto.AprovacaoTAP == null) { itemprojeto.AprovacaoTAP = string.Empty; }
                if (itemprojeto.AprovacaoTAPEnvio == null) { itemprojeto.AprovacaoTAPEnvio = string.Empty; }
                if (itemprojeto.AprovacaoTEP == null) { itemprojeto.AprovacaoTEP = string.Empty; }
                if (itemprojeto.AprovacaoTEPEnvio == null) { itemprojeto.AprovacaoTEPEnvio = string.Empty; }
                if (itemprojeto.ConcluidoPerc == null) { itemprojeto.ConcluidoPerc = string.Empty; }
                if (itemprojeto.Criado == null) { itemprojeto.Criado = string.Empty; }
                if (itemprojeto.DesvioPrazo == null) { itemprojeto.DesvioPrazo = string.Empty; }
                if (itemprojeto.DtInicioEncerramento == null) { itemprojeto.DtInicioEncerramento = string.Empty; }
                if (itemprojeto.DtInicioExecucao == null) { itemprojeto.DtInicioExecucao = string.Empty; }
                if (itemprojeto.DtInicioPlanejamento == null) { itemprojeto.DtInicioPlanejamento = string.Empty; }
                if (itemprojeto.DtSuspensao == null) { itemprojeto.DtSuspensao = string.Empty; }
                if (itemprojeto.DtTermino == null) { itemprojeto.DtTermino = string.Empty; }
                if (itemprojeto.Duracao == null) { itemprojeto.Duracao = string.Empty; }
                if (itemprojeto.E100Dias == null) { itemprojeto.E100Dias = string.Empty; }
                if (itemprojeto.EDTPlano100Dias == null) { itemprojeto.EDTPlano100Dias = string.Empty; }
                if (itemprojeto.Fase == null) { itemprojeto.Fase = string.Empty; }
                if (itemprojeto.Frente == null) { itemprojeto.Frente = string.Empty; }
                if (itemprojeto.Gerente == null) { itemprojeto.Gerente = string.Empty; }
                if (itemprojeto.Inicio == null) { itemprojeto.Inicio = string.Empty; }
                if (itemprojeto.Nome == null) { itemprojeto.Nome = string.Empty; }
                if (itemprojeto.NomePlano100Dias == null) { itemprojeto.NomePlano100Dias = string.Empty; }
                if (itemprojeto.PGP == null) { itemprojeto.PGP = string.Empty; }
                if (itemprojeto.PlanejadoPerc == null) { itemprojeto.PlanejadoPerc = string.Empty; }
                if (itemprojeto.QtdeLinhaBase == null) { itemprojeto.QtdeLinhaBase = string.Empty; }
                if (itemprojeto.Situacao == null) { itemprojeto.Situacao = string.Empty; }
                if (itemprojeto.Sponsor == null) { itemprojeto.Sponsor = string.Empty; }
                if (itemprojeto.TAP == null) { itemprojeto.TAP = string.Empty; }
                if (itemprojeto.TEP == null) { itemprojeto.TEP = string.Empty; }
                if (itemprojeto.Termino == null) { itemprojeto.Termino = string.Empty; }
                if (itemprojeto.TerminoLinhaBase == null) { itemprojeto.TerminoLinhaBase = string.Empty; }
                if (itemprojeto.Trabalho == null) { itemprojeto.Trabalho = string.Empty; }
                if (itemprojeto.Trimestre == null) { itemprojeto.Trimestre = string.Empty; }
                if (itemprojeto.UltimaModificacao == null) { itemprojeto.UltimaModificacao = string.Empty; }
                if (itemprojeto.UltimaPublicacao == null) { itemprojeto.UltimaPublicacao = string.Empty; }
                if (itemprojeto.UltimaRSP == null) { itemprojeto.UltimaRSP = string.Empty; }
            }
        }

        public void GravaListaIndicadores()
        {
            ProjectWebAppDataContext dc = new ProjectWebAppDataContext(
                new Uri(EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_ListData));
            dc.Credentials = new NetworkCredential(
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_User,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Password,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Domain);

            //deleta registros da lista
            foreach (IndicadoresPMOItem deleteitem in dc.IndicadoresPMO)
            {
                dc.DeleteObject(deleteitem);
                dc.SaveChanges();
            }

            //cria os novos registros gerados
            IndicadoresPMOItem novoitem = null;

            foreach (mdIndicador item in ListaIndicadorResult)
            {
                novoitem = new IndicadoresPMOItem();

                novoitem.NomeIndicador = item.NomeIndicador;
                novoitem.Titulo = item.Titulo;
                novoitem.Valor = item.Valor;
                novoitem.Status = item.Status;
                novoitem.Data = item.Data;

                dc.AddToIndicadoresPMO(novoitem);
                dc.SaveChanges();
            }        
        }

        public void GravaListaHistoricoIndicadores()
        {
            ProjectWebAppDataContext dc = new ProjectWebAppDataContext(
                new Uri(EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_ListData));
            dc.Credentials = new NetworkCredential(
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_User,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Password,
                EPMProjetosv1.Properties.Settings.Default.EPMProjetosv1_projectb2w_Project_Domain);

            //cria os novos registros gerados
            HistoricoIndicadoresItem novoitem = null;
            
            foreach (mdIndicador item in ListaIndicadorResult)
            {
                novoitem = new HistoricoIndicadoresItem();

                novoitem.NomeIndicador = item.NomeIndicador;
                novoitem.Titulo = item.Titulo;
                novoitem.Valor = item.Valor;
                novoitem.Status = item.Status;
                novoitem.Data = item.Data;

                dc.AddToHistoricoIndicadores(novoitem);
                dc.SaveChanges();
            }
        }








        #endregion


        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }
    }
}