package main

import (
	"context"
	"fmt"
	"log"
	"net"
	"strings"

	"rpcGoDatatype/csvconverter"
	pb "rpcGoDatatype/proto"

	"google.golang.org/grpc"
)

type server struct {
	pb.UnimplementedDataParserServer
}

func (s *server) Parse(ctx context.Context, req *pb.ParseRequest) (*pb.ParseResponse, error) {
	log.Printf("Parse request: from: %s, to: %s", req.From, req.To)

	var result string
	var err error

	switch {
	case strings.ToLower(req.From) == "csv" && strings.ToLower(req.To) == "json":
		result, err = csvconverter.ConvertCSVToJSON(req.Data)
		log.Printf("Converted CSV to JSON: %s", result)
	case strings.ToLower(req.From) == "json" && strings.ToLower(req.To) == "csv":
		result, err = csvconverter.ConvertJSONToCSV(req.Data)
	default:
		return nil, fmt.Errorf("unsupported conversion: from %s to %s", req.From, req.To)
	}

	if err != nil {
		return nil, err
	}

	return &pb.ParseResponse{
		Result: result,
	}, nil
}

func main() {
	lis, err := net.Listen("tcp", ":50051")
	if err != nil {
		log.Fatalf("failed to listen: %v", err)
	}

	s := grpc.NewServer()
	pb.RegisterDataParserServer(s, &server{})

	log.Printf("server listening at %v", lis.Addr())

	if err := s.Serve(lis); err != nil {
		log.Fatalf("failed to serve: %v", err)
	}
}
