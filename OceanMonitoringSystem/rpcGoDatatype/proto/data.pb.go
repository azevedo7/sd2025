// Code generated by protoc-gen-go. DO NOT EDIT.
// versions:
// 	protoc-gen-go v1.36.6
// 	protoc        v5.29.3
// source: proto/data.proto

package proto

import (
	protoreflect "google.golang.org/protobuf/reflect/protoreflect"
	protoimpl "google.golang.org/protobuf/runtime/protoimpl"
	reflect "reflect"
	sync "sync"
	unsafe "unsafe"
)

const (
	// Verify that this generated code is sufficiently up-to-date.
	_ = protoimpl.EnforceVersion(20 - protoimpl.MinVersion)
	// Verify that runtime/protoimpl is sufficiently up-to-date.
	_ = protoimpl.EnforceVersion(protoimpl.MaxVersion - 20)
)

type ParseRequest struct {
	state         protoimpl.MessageState `protogen:"open.v1"`
	From          string                 `protobuf:"bytes,1,opt,name=from,proto3" json:"from,omitempty"`
	To            string                 `protobuf:"bytes,2,opt,name=to,proto3" json:"to,omitempty"`
	Data          string                 `protobuf:"bytes,3,opt,name=data,proto3" json:"data,omitempty"`
	unknownFields protoimpl.UnknownFields
	sizeCache     protoimpl.SizeCache
}

func (x *ParseRequest) Reset() {
	*x = ParseRequest{}
	mi := &file_proto_data_proto_msgTypes[0]
	ms := protoimpl.X.MessageStateOf(protoimpl.Pointer(x))
	ms.StoreMessageInfo(mi)
}

func (x *ParseRequest) String() string {
	return protoimpl.X.MessageStringOf(x)
}

func (*ParseRequest) ProtoMessage() {}

func (x *ParseRequest) ProtoReflect() protoreflect.Message {
	mi := &file_proto_data_proto_msgTypes[0]
	if x != nil {
		ms := protoimpl.X.MessageStateOf(protoimpl.Pointer(x))
		if ms.LoadMessageInfo() == nil {
			ms.StoreMessageInfo(mi)
		}
		return ms
	}
	return mi.MessageOf(x)
}

// Deprecated: Use ParseRequest.ProtoReflect.Descriptor instead.
func (*ParseRequest) Descriptor() ([]byte, []int) {
	return file_proto_data_proto_rawDescGZIP(), []int{0}
}

func (x *ParseRequest) GetFrom() string {
	if x != nil {
		return x.From
	}
	return ""
}

func (x *ParseRequest) GetTo() string {
	if x != nil {
		return x.To
	}
	return ""
}

func (x *ParseRequest) GetData() string {
	if x != nil {
		return x.Data
	}
	return ""
}

type ParseResponse struct {
	state         protoimpl.MessageState `protogen:"open.v1"`
	Result        string                 `protobuf:"bytes,1,opt,name=result,proto3" json:"result,omitempty"`
	unknownFields protoimpl.UnknownFields
	sizeCache     protoimpl.SizeCache
}

func (x *ParseResponse) Reset() {
	*x = ParseResponse{}
	mi := &file_proto_data_proto_msgTypes[1]
	ms := protoimpl.X.MessageStateOf(protoimpl.Pointer(x))
	ms.StoreMessageInfo(mi)
}

func (x *ParseResponse) String() string {
	return protoimpl.X.MessageStringOf(x)
}

func (*ParseResponse) ProtoMessage() {}

func (x *ParseResponse) ProtoReflect() protoreflect.Message {
	mi := &file_proto_data_proto_msgTypes[1]
	if x != nil {
		ms := protoimpl.X.MessageStateOf(protoimpl.Pointer(x))
		if ms.LoadMessageInfo() == nil {
			ms.StoreMessageInfo(mi)
		}
		return ms
	}
	return mi.MessageOf(x)
}

// Deprecated: Use ParseResponse.ProtoReflect.Descriptor instead.
func (*ParseResponse) Descriptor() ([]byte, []int) {
	return file_proto_data_proto_rawDescGZIP(), []int{1}
}

func (x *ParseResponse) GetResult() string {
	if x != nil {
		return x.Result
	}
	return ""
}

var File_proto_data_proto protoreflect.FileDescriptor

const file_proto_data_proto_rawDesc = "" +
	"\n" +
	"\x10proto/data.proto\x12\x04data\"F\n" +
	"\fParseRequest\x12\x12\n" +
	"\x04from\x18\x01 \x01(\tR\x04from\x12\x0e\n" +
	"\x02to\x18\x02 \x01(\tR\x02to\x12\x12\n" +
	"\x04data\x18\x03 \x01(\tR\x04data\"'\n" +
	"\rParseResponse\x12\x16\n" +
	"\x06result\x18\x01 \x01(\tR\x06result2>\n" +
	"\n" +
	"DataParser\x120\n" +
	"\x05Parse\x12\x12.data.ParseRequest\x1a\x13.data.ParseResponseB\x1bZ\x19rpcGoDatatype/proto;protob\x06proto3"

var (
	file_proto_data_proto_rawDescOnce sync.Once
	file_proto_data_proto_rawDescData []byte
)

func file_proto_data_proto_rawDescGZIP() []byte {
	file_proto_data_proto_rawDescOnce.Do(func() {
		file_proto_data_proto_rawDescData = protoimpl.X.CompressGZIP(unsafe.Slice(unsafe.StringData(file_proto_data_proto_rawDesc), len(file_proto_data_proto_rawDesc)))
	})
	return file_proto_data_proto_rawDescData
}

var file_proto_data_proto_msgTypes = make([]protoimpl.MessageInfo, 2)
var file_proto_data_proto_goTypes = []any{
	(*ParseRequest)(nil),  // 0: data.ParseRequest
	(*ParseResponse)(nil), // 1: data.ParseResponse
}
var file_proto_data_proto_depIdxs = []int32{
	0, // 0: data.DataParser.Parse:input_type -> data.ParseRequest
	1, // 1: data.DataParser.Parse:output_type -> data.ParseResponse
	1, // [1:2] is the sub-list for method output_type
	0, // [0:1] is the sub-list for method input_type
	0, // [0:0] is the sub-list for extension type_name
	0, // [0:0] is the sub-list for extension extendee
	0, // [0:0] is the sub-list for field type_name
}

func init() { file_proto_data_proto_init() }
func file_proto_data_proto_init() {
	if File_proto_data_proto != nil {
		return
	}
	type x struct{}
	out := protoimpl.TypeBuilder{
		File: protoimpl.DescBuilder{
			GoPackagePath: reflect.TypeOf(x{}).PkgPath(),
			RawDescriptor: unsafe.Slice(unsafe.StringData(file_proto_data_proto_rawDesc), len(file_proto_data_proto_rawDesc)),
			NumEnums:      0,
			NumMessages:   2,
			NumExtensions: 0,
			NumServices:   1,
		},
		GoTypes:           file_proto_data_proto_goTypes,
		DependencyIndexes: file_proto_data_proto_depIdxs,
		MessageInfos:      file_proto_data_proto_msgTypes,
	}.Build()
	File_proto_data_proto = out.File
	file_proto_data_proto_goTypes = nil
	file_proto_data_proto_depIdxs = nil
}
