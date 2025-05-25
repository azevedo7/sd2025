package csvconverter

import (
	"encoding/csv"
	"encoding/json"
	"fmt"
	"strings"
)

func ConvertJSONToCSV(jsonString string) (string, error) {
	// Parse JSON array of objects
	var data []map[string]interface{}
	if err := json.Unmarshal([]byte(jsonString), &data); err != nil {
		return "", fmt.Errorf("error parsing JSON: %v", err)
	}

	if len(data) == 0 {
		return "", fmt.Errorf("empty JSON array")
	}

	// Get headers from first object
	headers := make([]string, 0)
	for key := range data[0] {
		headers = append(headers, key)
	}

	// Create CSV writer
	var csvBuilder strings.Builder
	writer := csv.NewWriter(&csvBuilder)

	// Write headers
	if err := writer.Write(headers); err != nil {
		return "", fmt.Errorf("error writing headers: %v", err)
	}

	// Write data rows
	for _, item := range data {
		row := make([]string, len(headers))
		for i, header := range headers {
			value := item[header]
			// Convert value to string
			if value == nil {
				row[i] = ""
			} else {
				row[i] = fmt.Sprintf("%v", value)
			}
		}
		if err := writer.Write(row); err != nil {
			return "", fmt.Errorf("error writing row: %v", err)
		}
	}

	writer.Flush()
	if err := writer.Error(); err != nil {
		return "", fmt.Errorf("error flushing CSV: %v", err)
	}

	return csvBuilder.String(), nil
}
