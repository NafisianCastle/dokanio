using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Security monitoring service for threat detection and security event analysis
/// </summary>
public class SecurityMonitoringService : ISecurityMonitoringService
{
    private readonly IAuditService _auditService;
    private readonly ILogger<SecurityMonitoringService> _logger;
    private readonly Dictionary<string, ThreatPattern> _threatPatterns;
    private readonly Dictionary<Guid, SecurityMetrics> _businessSecurityMetrics;

    public SecurityMonitoringService(
        IAuditService auditService,
        ILogger<SecurityMonitoringService> logger)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _threatPatterns = InitializeThreatPatterns();
        _businessSecurityMetrics = new Dictionary<Guid, SecurityMetrics>();
    }

    /// <summary>
    /// Monitors security events in real-time and detects potential threats
    /// </summary>
    public async Task<ThreatDetectionResult> MonitorSecurityEventsAsync(Guid businessId, TimeSpan monitoringWindow)
    {
        try
        {
            _logger.LogInformation("Starting security monitoring for business {BusinessId} with {Window} window", 
                businessId, monitoringWindow);

            var fromDate = DateTime.UtcNow.Subtract(monitoringWindow);
            var auditLogs = await _auditService.GetAuditLogsAsync(fromDate, DateTime.UtcNow);
            
            // Filter logs for the specific business
            var businessLogs = auditLogs.Where(log => 
                IsBusinessRelated(log, businessId)).ToList();

            var threats = new List<DetectedThreat>();
            var securityAlerts = new List<SecurityAlert>();

            // Analyze for different threat patterns
            foreach (var pattern in _threatPatterns.Values)
            {
                var patternThreats = await AnalyzeThreatPattern(pattern, businessLogs, businessId);
                threats.AddRange(patternThreats);
            }

            // Generate security alerts based on detected threats
            foreach (var threat in threats.Where(t => t.Severity >= ThreatSeverity.Medium))
            {
                var alert = await GenerateSecurityAlert(threat, businessId);
                securityAlerts.Add(alert);
            }

            // Update security metrics
            await UpdateSecurityMetrics(businessId, threats, securityAlerts);

            var result = new ThreatDetectionResult
            {
                BusinessId = businessId,
                MonitoringWindow = monitoringWindow,
                DetectedThreats = threats,
                SecurityAlerts = securityAlerts,
                TotalEventsAnalyzed = businessLogs.Count,
                HighSeverityThreats = threats.Count(t => t.Severity == ThreatSeverity.High),
                MediumSeverityThreats = threats.Count(t => t.Severity == ThreatSeverity.Medium),
                LowSeverityThreats = threats.Count(t => t.Severity == ThreatSeverity.Low),
                MonitoringTimestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Security monitoring completed for business {BusinessId}. Threats: {ThreatCount}, Alerts: {AlertCount}",
                businessId, threats.Count, securityAlerts.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during security monitoring for business {BusinessId}", businessId);
            throw;
        }
    }

    /// <summary>
    /// Analyzes authentication patterns for suspicious activity
    /// </summary>
    public async Task<AuthenticationAnalysisResult> AnalyzeAuthenticationPatternsAsync(Guid businessId, TimeSpan analysisWindow)
    {
        try
        {
            var fromDate = DateTime.UtcNow.Subtract(analysisWindow);
            var authLogs = await _auditService.GetActionAuditLogsAsync(AuditAction.Login, fromDate, DateTime.UtcNow);
            var logoutLogs = await _auditService.GetActionAuditLogsAsync(AuditAction.Logout, fromDate, DateTime.UtcNow);
            var failedAuthLogs = await _auditService.GetActionAuditLogsAsync(AuditAction.SecurityViolation, fromDate, DateTime.UtcNow);

            // Filter for business-related authentication events
            var businessAuthLogs = authLogs.Where(log => IsBusinessRelated(log, businessId)).ToList();
            var businessLogoutLogs = logoutLogs.Where(log => IsBusinessRelated(log, businessId)).ToList();
            var businessFailedLogs = failedAuthLogs.Where(log => 
                IsBusinessRelated(log, businessId) && 
                log.Description.Contains("authentication", StringComparison.OrdinalIgnoreCase)).ToList();

            var suspiciousPatterns = new List<SuspiciousAuthPattern>();

            // Detect multiple failed login attempts
            var failedAttemptsByUser = businessFailedLogs
                .Where(log => log.UserId.HasValue)
                .GroupBy(log => log.UserId.Value)
                .Where(group => group.Count() >= 5) // 5 or more failed attempts
                .ToList();

            foreach (var userGroup in failedAttemptsByUser)
            {
                suspiciousPatterns.Add(new SuspiciousAuthPattern
                {
                    PatternType = "MultipleFailedLogins",
                    UserId = userGroup.Key,
                    EventCount = userGroup.Count(),
                    FirstOccurrence = userGroup.Min(log => log.CreatedAt),
                    LastOccurrence = userGroup.Max(log => log.CreatedAt),
                    Severity = userGroup.Count() >= 10 ? ThreatSeverity.High : ThreatSeverity.Medium,
                    Description = $"User attempted login {userGroup.Count()} times unsuccessfully"
                });
            }

            // Detect unusual login times (outside business hours)
            var unusualTimeLogins = businessAuthLogs
                .Where(log => IsUnusualLoginTime(log.CreatedAt))
                .ToList();

            if (unusualTimeLogins.Count >= 3)
            {
                suspiciousPatterns.Add(new SuspiciousAuthPattern
                {
                    PatternType = "UnusualLoginTimes",
                    EventCount = unusualTimeLogins.Count,
                    FirstOccurrence = unusualTimeLogins.Min(log => log.CreatedAt),
                    LastOccurrence = unusualTimeLogins.Max(log => log.CreatedAt),
                    Severity = ThreatSeverity.Medium,
                    Description = $"{unusualTimeLogins.Count} logins detected outside normal business hours"
                });
            }

            // Detect concurrent sessions from different locations (if IP addresses are available)
            var concurrentSessions = DetectConcurrentSessions(businessAuthLogs, businessLogoutLogs);
            suspiciousPatterns.AddRange(concurrentSessions);

            var result = new AuthenticationAnalysisResult
            {
                BusinessId = businessId,
                AnalysisWindow = analysisWindow,
                TotalLoginAttempts = businessAuthLogs.Count,
                SuccessfulLogins = businessAuthLogs.Count,
                FailedLogins = businessFailedLogs.Count,
                SuspiciousPatterns = suspiciousPatterns,
                UnusualActivityDetected = suspiciousPatterns.Any(p => p.Severity >= ThreatSeverity.Medium),
                AnalysisTimestamp = DateTime.UtcNow
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing authentication patterns for business {BusinessId}", businessId);
            throw;
        }
    }

    /// <summary>
    /// Generates security compliance report for data protection regulations
    /// </summary>
    public async Task<ComplianceReport> GenerateComplianceReportAsync(Guid businessId, ComplianceStandard standard, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var from = fromDate ?? DateTime.UtcNow.AddMonths(-1);
            var to = toDate ?? DateTime.UtcNow;

            _logger.LogInformation("Generating {Standard} compliance report for business {BusinessId} from {From} to {To}",
                standard, businessId, from, to);

            var auditLogs = await _auditService.GetAuditLogsAsync(from, to);
            var businessLogs = auditLogs.Where(log => IsBusinessRelated(log, businessId)).ToList();

            var complianceChecks = new List<ComplianceCheck>();

            switch (standard)
            {
                case ComplianceStandard.GDPR:
                    complianceChecks = await PerformGDPRComplianceChecks(businessLogs, businessId);
                    break;
                case ComplianceStandard.PCI_DSS:
                    complianceChecks = await PerformPCIDSSComplianceChecks(businessLogs, businessId);
                    break;
                case ComplianceStandard.SOX:
                    complianceChecks = await PerformSOXComplianceChecks(businessLogs, businessId);
                    break;
                default:
                    complianceChecks = await PerformGeneralComplianceChecks(businessLogs, businessId);
                    break;
            }

            var passedChecks = complianceChecks.Count(c => c.Status == ComplianceStatus.Compliant);
            var failedChecks = complianceChecks.Count(c => c.Status == ComplianceStatus.NonCompliant);
            var warningChecks = complianceChecks.Count(c => c.Status == ComplianceStatus.Warning);

            var overallScore = complianceChecks.Count > 0 
                ? (int)((double)passedChecks / complianceChecks.Count * 100)
                : 100;

            var report = new ComplianceReport
            {
                BusinessId = businessId,
                Standard = standard,
                ReportPeriod = new DTOs.DateRange { StartDate = from, EndDate = to },
                ComplianceChecks = complianceChecks,
                OverallComplianceScore = overallScore,
                PassedChecks = passedChecks,
                FailedChecks = failedChecks,
                WarningChecks = warningChecks,
                RecommendedActions = GenerateComplianceRecommendations(complianceChecks),
                GeneratedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Compliance report generated for business {BusinessId}. Score: {Score}%, Passed: {Passed}, Failed: {Failed}",
                businessId, overallScore, passedChecks, failedChecks);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for business {BusinessId}", businessId);
            throw;
        }
    }

    /// <summary>
    /// Configures security monitoring rules and thresholds
    /// </summary>
    public async Task<SecurityConfigurationResult> ConfigureSecurityMonitoringAsync(Guid businessId, SecurityMonitoringConfig config)
    {
        try
        {
            _logger.LogInformation("Configuring security monitoring for business {BusinessId}", businessId);

            // Store configuration (in a real implementation, this would be persisted)
            var configResult = new SecurityConfigurationResult
            {
                BusinessId = businessId,
                Configuration = config,
                ConfiguredAt = DateTime.UtcNow,
                Success = true
            };

            // Validate configuration
            var validationErrors = ValidateSecurityConfiguration(config);
            if (validationErrors.Any())
            {
                configResult.Success = false;
                configResult.ValidationErrors = validationErrors;
                configResult.Message = "Configuration validation failed";
                return configResult;
            }

            // Apply configuration to threat patterns
            foreach (var rule in config.ThreatDetectionRules)
            {
                if (_threatPatterns.ContainsKey(rule.PatternName))
                {
                    _threatPatterns[rule.PatternName].Threshold = rule.Threshold;
                    _threatPatterns[rule.PatternName].Severity = rule.Severity;
                }
            }

            configResult.Message = "Security monitoring configured successfully";
            configResult.EnabledFeatures = config.ThreatDetectionRules.Select(r => r.PatternName).ToList();

            return configResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring security monitoring for business {BusinessId}", businessId);
            return new SecurityConfigurationResult
            {
                BusinessId = businessId,
                Success = false,
                Message = $"Configuration error: {ex.Message}",
                ConfiguredAt = DateTime.UtcNow
            };
        }
    }

    private Dictionary<string, ThreatPattern> InitializeThreatPatterns()
    {
        return new Dictionary<string, ThreatPattern>
        {
            ["BruteForceLogin"] = new ThreatPattern
            {
                Name = "BruteForceLogin",
                Description = "Multiple failed login attempts from same user/IP",
                Threshold = 5,
                TimeWindow = TimeSpan.FromMinutes(15),
                Severity = ThreatSeverity.High,
                Actions = new[] { AuditAction.SecurityViolation }
            },
            ["UnusualDataAccess"] = new ThreatPattern
            {
                Name = "UnusualDataAccess",
                Description = "Access to sensitive data outside normal patterns",
                Threshold = 10,
                TimeWindow = TimeSpan.FromHours(1),
                Severity = ThreatSeverity.Medium,
                Actions = new[] { AuditAction.DataDecryption, AuditAction.DataAccess }
            },
            ["PrivilegeEscalation"] = new ThreatPattern
            {
                Name = "PrivilegeEscalation",
                Description = "Attempts to access resources beyond user permissions",
                Threshold = 3,
                TimeWindow = TimeSpan.FromMinutes(30),
                Severity = ThreatSeverity.High,
                Actions = new[] { AuditAction.SecurityViolation }
            },
            ["DataExfiltration"] = new ThreatPattern
            {
                Name = "DataExfiltration",
                Description = "Large volume of data access or export",
                Threshold = 100,
                TimeWindow = TimeSpan.FromHours(1),
                Severity = ThreatSeverity.High,
                Actions = new[] { AuditAction.DataExport, AuditAction.DataAccess }
            }
        };
    }

    private async Task<List<DetectedThreat>> AnalyzeThreatPattern(ThreatPattern pattern, List<AuditLog> logs, Guid businessId)
    {
        var threats = new List<DetectedThreat>();
        var patternLogs = logs.Where(log => pattern.Actions.Contains(log.Action)).ToList();

        if (!patternLogs.Any()) return threats;

        // Group by time windows and analyze
        var timeWindows = patternLogs
            .GroupBy(log => new DateTime(log.CreatedAt.Ticks / pattern.TimeWindow.Ticks * pattern.TimeWindow.Ticks))
            .Where(group => group.Count() >= pattern.Threshold);

        foreach (var window in timeWindows)
        {
            var threat = new DetectedThreat
            {
                ThreatType = pattern.Name,
                Description = pattern.Description,
                Severity = pattern.Severity,
                BusinessId = businessId,
                DetectedAt = DateTime.UtcNow,
                EventCount = window.Count(),
                TimeWindow = pattern.TimeWindow,
                AffectedUsers = window.Where(log => log.UserId.HasValue).Select(log => log.UserId!.Value).Distinct().ToList(),
                Evidence = window.Take(5).Select(log => $"{log.CreatedAt}: {log.Description}").ToList()
            };

            threats.Add(threat);
        }

        return threats;
    }

    private async Task<SecurityAlert> GenerateSecurityAlert(DetectedThreat threat, Guid businessId)
    {
        var alert = new SecurityAlert
        {
            AlertId = Guid.NewGuid(),
            BusinessId = businessId,
            ThreatType = threat.ThreatType,
            Severity = threat.Severity,
            Title = $"Security Threat Detected: {threat.ThreatType}",
            Description = threat.Description,
            RecommendedActions = GetRecommendedActions(threat.ThreatType),
            CreatedAt = DateTime.UtcNow,
            Status = AlertStatus.Active,
            AffectedUsers = threat.AffectedUsers
        };

        // Log the security alert
        await _auditService.LogAsync(
            null,
            AuditAction.SecurityAlert,
            $"Security alert generated: {alert.Title}",
            nameof(SecurityAlert),
            alert.AlertId);

        return alert;
    }

    private async Task UpdateSecurityMetrics(Guid businessId, List<DetectedThreat> threats, List<SecurityAlert> alerts)
    {
        if (!_businessSecurityMetrics.ContainsKey(businessId))
        {
            _businessSecurityMetrics[businessId] = new SecurityMetrics { BusinessId = businessId };
        }

        var metrics = _businessSecurityMetrics[businessId];
        metrics.TotalThreatsDetected += threats.Count;
        metrics.HighSeverityThreats += threats.Count(t => t.Severity == ThreatSeverity.High);
        metrics.MediumSeverityThreats += threats.Count(t => t.Severity == ThreatSeverity.Medium);
        metrics.LowSeverityThreats += threats.Count(t => t.Severity == ThreatSeverity.Low);
        metrics.ActiveAlerts += alerts.Count;
        metrics.LastMonitoringRun = DateTime.UtcNow;
    }

    private bool IsBusinessRelated(AuditLog log, Guid businessId)
    {
        // Check if the log is related to the specific business
        return log.Description.Contains(businessId.ToString()) ||
               log.EntityType == nameof(Business) ||
               log.EntityType == nameof(Shop) ||
               log.EntityType == nameof(Product) ||
               log.EntityType == nameof(Sale);
    }

    private bool IsUnusualLoginTime(DateTime loginTime)
    {
        // Consider logins outside 6 AM - 10 PM as unusual
        var hour = loginTime.Hour;
        return hour < 6 || hour > 22;
    }

    private List<SuspiciousAuthPattern> DetectConcurrentSessions(List<AuditLog> loginLogs, List<AuditLog> logoutLogs)
    {
        var patterns = new List<SuspiciousAuthPattern>();
        
        // Group by user and detect overlapping sessions
        var userLogins = loginLogs.Where(log => log.UserId.HasValue).GroupBy(log => log.UserId.Value);
        
        foreach (var userGroup in userLogins)
        {
            var userLogouts = logoutLogs.Where(log => log.UserId == userGroup.Key).ToList();
            var sessions = new List<(DateTime Start, DateTime? End, string? IpAddress)>();
            
            foreach (var login in userGroup.OrderBy(log => log.CreatedAt))
            {
                var logout = userLogouts.FirstOrDefault(log => log.CreatedAt > login.CreatedAt);
                sessions.Add((login.CreatedAt, logout?.CreatedAt, login.IpAddress));
            }
            
            // Check for overlapping sessions with different IP addresses
            for (int i = 0; i < sessions.Count - 1; i++)
            {
                var current = sessions[i];
                var next = sessions[i + 1];
                
                if (!current.End.HasValue || next.Start < current.End.Value)
                {
                    if (!string.IsNullOrEmpty(current.IpAddress) && 
                        !string.IsNullOrEmpty(next.IpAddress) && 
                        current.IpAddress != next.IpAddress)
                    {
                        patterns.Add(new SuspiciousAuthPattern
                        {
                            PatternType = "ConcurrentSessions",
                            UserId = userGroup.Key,
                            EventCount = 2,
                            FirstOccurrence = current.Start,
                            LastOccurrence = next.Start,
                            Severity = ThreatSeverity.High,
                            Description = $"Concurrent sessions detected from different IP addresses: {current.IpAddress} and {next.IpAddress}"
                        });
                    }
                }
            }
        }
        
        return patterns;
    }

    private async Task<List<ComplianceCheck>> PerformGDPRComplianceChecks(List<AuditLog> logs, Guid businessId)
    {
        var checks = new List<ComplianceCheck>();

        // Check for data encryption
        var encryptionLogs = logs.Where(log => log.Action == AuditAction.DataEncryption).ToList();
        checks.Add(new ComplianceCheck
        {
            CheckName = "Data Encryption",
            Description = "Verify that personal data is encrypted",
            Status = encryptionLogs.Any() ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
            Evidence = $"{encryptionLogs.Count} encryption events found",
            Requirement = "GDPR Article 32 - Security of processing"
        });

        // Check for audit logging
        checks.Add(new ComplianceCheck
        {
            CheckName = "Audit Logging",
            Description = "Verify comprehensive audit logging is in place",
            Status = logs.Count > 0 ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
            Evidence = $"{logs.Count} audit events recorded",
            Requirement = "GDPR Article 30 - Records of processing activities"
        });

        // Check for data access controls
        var accessViolations = logs.Where(log => log.Action == AuditAction.SecurityViolation).ToList();
        checks.Add(new ComplianceCheck
        {
            CheckName = "Access Controls",
            Description = "Verify proper access controls are enforced",
            Status = accessViolations.Count < 5 ? ComplianceStatus.Compliant : ComplianceStatus.Warning,
            Evidence = $"{accessViolations.Count} access violations detected",
            Requirement = "GDPR Article 32 - Security of processing"
        });

        return checks;
    }

    private async Task<List<ComplianceCheck>> PerformPCIDSSComplianceChecks(List<AuditLog> logs, Guid businessId)
    {
        var checks = new List<ComplianceCheck>();

        // Check for secure authentication
        var authLogs = logs.Where(log => log.Action == AuditAction.Login).ToList();
        checks.Add(new ComplianceCheck
        {
            CheckName = "Secure Authentication",
            Description = "Verify secure authentication mechanisms",
            Status = authLogs.Any() ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
            Evidence = $"{authLogs.Count} authentication events logged",
            Requirement = "PCI DSS Requirement 8 - Identify and authenticate access"
        });

        return checks;
    }

    private async Task<List<ComplianceCheck>> PerformSOXComplianceChecks(List<AuditLog> logs, Guid businessId)
    {
        var checks = new List<ComplianceCheck>();

        // Check for financial transaction logging
        var financialLogs = logs.Where(log => 
            log.EntityType == nameof(Sale) || 
            log.Description.Contains("financial", StringComparison.OrdinalIgnoreCase)).ToList();
        
        checks.Add(new ComplianceCheck
        {
            CheckName = "Financial Transaction Logging",
            Description = "Verify all financial transactions are logged",
            Status = financialLogs.Any() ? ComplianceStatus.Compliant : ComplianceStatus.Warning,
            Evidence = $"{financialLogs.Count} financial transaction events logged",
            Requirement = "SOX Section 404 - Internal controls"
        });

        return checks;
    }

    private async Task<List<ComplianceCheck>> PerformGeneralComplianceChecks(List<AuditLog> logs, Guid businessId)
    {
        var checks = new List<ComplianceCheck>();

        checks.Add(new ComplianceCheck
        {
            CheckName = "General Audit Logging",
            Description = "Verify audit logging is functioning",
            Status = logs.Count > 0 ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant,
            Evidence = $"{logs.Count} audit events recorded",
            Requirement = "General security best practices"
        });

        return checks;
    }

    private List<string> GenerateComplianceRecommendations(List<ComplianceCheck> checks)
    {
        var recommendations = new List<string>();

        foreach (var check in checks.Where(c => c.Status != ComplianceStatus.Compliant))
        {
            switch (check.CheckName)
            {
                case "Data Encryption":
                    recommendations.Add("Implement comprehensive data encryption for all sensitive data");
                    break;
                case "Audit Logging":
                    recommendations.Add("Enable comprehensive audit logging for all system activities");
                    break;
                case "Access Controls":
                    recommendations.Add("Review and strengthen access control mechanisms");
                    break;
                case "Secure Authentication":
                    recommendations.Add("Implement multi-factor authentication and secure password policies");
                    break;
                case "Financial Transaction Logging":
                    recommendations.Add("Ensure all financial transactions are properly logged and auditable");
                    break;
            }
        }

        return recommendations;
    }

    private List<string> GetRecommendedActions(string threatType)
    {
        return threatType switch
        {
            "BruteForceLogin" => new List<string>
            {
                "Temporarily lock the affected user account",
                "Review authentication logs for the user",
                "Consider implementing account lockout policies",
                "Notify the user of suspicious activity"
            },
            "UnusualDataAccess" => new List<string>
            {
                "Review data access patterns for the user",
                "Verify the legitimacy of data access",
                "Consider additional access controls",
                "Monitor user activity closely"
            },
            "PrivilegeEscalation" => new List<string>
            {
                "Immediately review user permissions",
                "Audit recent permission changes",
                "Investigate the source of escalation attempts",
                "Consider revoking elevated privileges"
            },
            "DataExfiltration" => new List<string>
            {
                "Immediately investigate data access patterns",
                "Review export/download activities",
                "Consider blocking data export capabilities",
                "Notify security team immediately"
            },
            _ => new List<string> { "Review security logs and investigate further" }
        };
    }

    private List<string> ValidateSecurityConfiguration(SecurityMonitoringConfig config)
    {
        var errors = new List<string>();

        if (config.ThreatDetectionRules == null || !config.ThreatDetectionRules.Any())
        {
            errors.Add("At least one threat detection rule must be configured");
        }

        foreach (var rule in config.ThreatDetectionRules ?? new List<ThreatDetectionRule>())
        {
            if (string.IsNullOrEmpty(rule.PatternName))
            {
                errors.Add("Threat detection rule must have a pattern name");
            }

            if (rule.Threshold <= 0)
            {
                errors.Add($"Threshold for rule '{rule.PatternName}' must be greater than 0");
            }
        }

        return errors;
    }
}