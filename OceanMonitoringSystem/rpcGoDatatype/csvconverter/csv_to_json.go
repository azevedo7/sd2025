package csvconverter

import (
	"encoding/csv"
	"encoding/json"
	"fmt"
	"strconv"
	"strings"
)

func isNumber(s string) bool {
	_, err := strconv.ParseFloat(s, 64)
	return err == nil
}

func ConvertCSVToJSON(csvString string) (string, error) {
	reader := csv.NewReader(strings.NewReader(csvString))

	headers, err := reader.Read()
	if err != nil {
		return "", fmt.Errorf("error reading headers: %v", err)
	}

	records, err := reader.ReadAll()
	if err != nil {
		return "", fmt.Errorf("error reading records: %v", err)
	}

	var data []map[string]interface{}

	for _, row := range records {
		item := make(map[string]interface{})
		for i, value := range row {
			if isNumber(value) {
				num, _ := strconv.ParseFloat(value, 64)
				item[headers[i]] = num
			} else {
				item[headers[i]] = value
			}
		}
		data = append(data, item)
	}

	jsonData, err := json.MarshalIndent(data, "", "  ")
	if err != nil {
		return "", fmt.Errorf("error converting to JSON: %v", err)
	}

	return string(jsonData), nil
}
