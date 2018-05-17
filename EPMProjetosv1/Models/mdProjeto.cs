namespace EPMProjetosv1.Models
{
    using System;

    public class mdProjeto
    {
        public Guid ProjetoId { get; set; }
        public string Fase { get; set; }
        public string Nome { get; set; }
        public string ConcluidoPerc { get; set; }
        public string PlanejadoPerc { get; set; }
        public string DesvioPrazo { get; set; }
        public string Situacao { get; set; }
        public string Inicio { get; set; }
        public string Termino { get; set; }
        public string Gerente { get; set; }
        public string QtdeLinhaBase { get; set; }
        public string TerminoLinhaBase { get; set; }
        public string Duracao { get; set; }
        public string Trabalho { get; set; }
        public string EDTPlano100Dias { get; set; }
        public string NomePlano100Dias { get; set; }
        public string Sponsor { get; set; }
        public string TAP { get; set; }
        public string AprovacaoTAPEnvio { get; set; }
        public string AprovacaoTAP { get; set; }
        public string PGP { get; set; }
        public string AprovacaoPGPEnvio { get; set; }
        public string AprovacaoPGP { get; set; }
        public string UltimaRSP { get; set; }
        public string TEP { get; set; }
        public string AprovacaoTEPEnvio { get; set; }
        public string AprovacaoTEP { get; set; }
        public string Frente { get; set; }
        public string Trimestre { get; set; }
        public string Criado { get; set; }
        public string DtInicioPlanejamento { get; set; }
        public string DtInicioExecucao { get; set; }
        public string DtInicioEncerramento { get; set; }
        public string DtTermino { get; set; }
        public string DtSuspensao { get; set; }
        public string UltimaModificacao { get; set; }
        public string UltimaPublicacao { get; set; }
        public string E100Dias { get; set; }
    }
}