package logger

import (
	"fmt"
	"io"
	"os"
	"sync/atomic"
	"time"
)

// Logger provides structured logging capabilities for the CLI
// Equivalent to C# OctoLogger
type Logger struct {
	verbose       bool
	output        io.Writer
	verboseOutput io.Writer
	warningCount  atomic.Int32
}

// New creates a new Logger instance
func New(verbose bool, outputs ...io.Writer) *Logger {
	output := io.Writer(os.Stdout)
	verboseOutput := io.Writer(os.Stdout)

	if len(outputs) > 0 && outputs[0] != nil {
		output = outputs[0]
	}
	if len(outputs) > 1 && outputs[1] != nil {
		verboseOutput = outputs[1]
	}

	return &Logger{
		verbose:       verbose,
		output:        output,
		verboseOutput: verboseOutput,
	}
}

// Debug logs a debug message
func (l *Logger) Debug(format string, args ...interface{}) {
	l.log("[DEBUG]", format, args...)
}

// Info logs an informational message
func (l *Logger) Info(format string, args ...interface{}) {
	l.log("[INFO]", format, args...)
}

// Success logs a success message
func (l *Logger) Success(format string, args ...interface{}) {
	l.log("[SUCCESS]", format, args...)
}

// Warning logs a warning message and increments warning count
func (l *Logger) Warning(format string, args ...interface{}) {
	l.warningCount.Add(1)
	l.log("[WARNING]", format, args...)
}

// Error logs an error
func (l *Logger) Error(err error) {
	if err != nil {
		l.log("[ERROR]", "%v", err)
	}
}

// Errorf logs a formatted error message
func (l *Logger) Errorf(format string, args ...interface{}) {
	l.log("[ERROR]", format, args...)
}

// Verbose logs a verbose message (only when verbose mode is enabled)
func (l *Logger) Verbose(format string, args ...interface{}) {
	if l.verbose {
		msg := fmt.Sprintf(format, args...)
		timestamp := time.Now().Format("2006-01-02 15:04:05")
		fmt.Fprintf(l.verboseOutput, "[VERBOSE] %s: %s\n", timestamp, msg)
	}
}

// GetWarningCount returns the number of warnings logged
func (l *Logger) GetWarningCount() int {
	return int(l.warningCount.Load())
}

// LogWarningCount logs the total warning count if warnings occurred
func (l *Logger) LogWarningCount() {
	count := l.GetWarningCount()
	if count > 0 {
		l.Warning("Total warnings: %d", count)
	}
}

func (l *Logger) log(level, format string, args ...interface{}) {
	msg := fmt.Sprintf(format, args...)
	timestamp := time.Now().Format("2006-01-02 15:04:05")
	fmt.Fprintf(l.output, "%s %s: %s\n", level, timestamp, msg)
}
