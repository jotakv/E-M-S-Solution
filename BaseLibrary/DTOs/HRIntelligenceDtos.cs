namespace BaseLibrary.DTOs
{
    public class EmployeeRiskDto
    {
        public int    EmployeeId       { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public string Department       { get; set; } = string.Empty;
        public string Branch           { get; set; } = string.Empty;
        public int    RiskScore        { get; set; }
        public string RiskLevel        { get; set; } = string.Empty; // "Low" | "Medium" | "High"
        public int    OvertimeCount    { get; set; }
        public int    SickLeaveCount   { get; set; }
        public int    SanctionCount    { get; set; }
        public int    NegativeNoteCount { get; set; }
        public int    PositiveNoteCount { get; set; }
        public List<string> RiskReasons { get; set; } = new();
    }

    public class SentimentSummaryDto
    {
        public int    TotalNotes    { get; set; }
        public int    PositiveCount { get; set; }
        public int    NeutralCount  { get; set; }
        public int    NegativeCount { get; set; }
        public double PositivePct   { get; set; }
        public double NeutralPct    { get; set; }
        public double NegativePct   { get; set; }
    }

    public class SentimentTrendDto
    {
        public string PeriodLabel  { get; set; } = string.Empty;
        public double PositivePct  { get; set; }
        public double NeutralPct   { get; set; }
        public double NegativePct  { get; set; }
    }

    public class DepartmentMoraleDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public double PositivePct    { get; set; }
        public double NeutralPct     { get; set; }
        public double NegativePct    { get; set; }
    }

    public class CreateNoteRequest
    {
        public int    EmployeeId       { get; set; }
        public string NoteText         { get; set; } = string.Empty;
        public string CreatedByUserId  { get; set; } = string.Empty;
    }

    public class NoteCreatedResponse
    {
        public int    NoteId         { get; set; }
        public string SentimentLabel { get; set; } = string.Empty;
        public float  SentimentScore { get; set; }
    }

    public class EmployeeNoteDto
    {
        public int      Id              { get; set; }
        public int      EmployeeId      { get; set; }
        public string   EmployeeName    { get; set; } = string.Empty;
        public string   Department      { get; set; } = string.Empty;
        public string   Branch          { get; set; } = string.Empty;
        public string   NoteText        { get; set; } = string.Empty;
        public float    SentimentScore  { get; set; }
        public string   SentimentLabel  { get; set; } = string.Empty;
        public DateTime CreatedAt       { get; set; }
        public string   CreatedByUserId { get; set; } = string.Empty;
    }

    public class PagedNotesResponse
    {
        public List<EmployeeNoteDto> Notes      { get; set; } = new();
        public int                   TotalCount  { get; set; }
        public int                   Page        { get; set; }
        public int                   PageSize    { get; set; }
    }
}
