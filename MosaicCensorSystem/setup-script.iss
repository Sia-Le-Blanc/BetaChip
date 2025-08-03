<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- 
    Microsoft ResX Schema 
    
    Version 2.0
    
    The primary goals of this format is to allow a simple XML format 
    that is mostly human readable. The generation and parsing of the 
    various data types are done through the TypeConverter classes 
    associated with the data types.
    
    Example:
    
    ... ado.net/XML headers & schema ...
    <resheader name="resmimetype">text/microsoft-resx</resheader>
    <resheader name="version">2.0</resheader>
    <resheader name="reader">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
    <resheader name="writer">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
    <data name="Name1"><value>this is my long string</value><comment>this is a comment</comment></data>
    <data name="Color1" type="System.Drawing.Color, System.Drawing">Blue</data>
    <data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64">
        <value>[base64 mime encoded serialized .NET Framework object]</value>
    </data>
    <data name="Icon1" type="System.Drawing.Icon, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64">
        <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>
        <comment>This is a comment</comment>
    </data>
                
    There are any number of "resheader" rows that contain simple 
    name/value pairs.
    
    Each data row contains a name, and value. The row also contains a 
    type or mimetype. Type corresponds to a .NET class that support 
    text/value conversion. The mimetype is used for serialized objects, 
    and tells the ResXResourceReader how to depersist the object. This is currently not 
    extensible. For a given mimetype the value must be set accordingly:
    
    Note - application/x-microsoft.net.object.binary.base64 is the format 
    that the ResXResourceWriter will generate, however the reader can 
    read any of the formats listed below.
    
    mimetype: application/x-microsoft.net.object.binary.base64
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
            : and then encoded with base64 encoding.
    
    mimetype: application/x-microsoft.net.object.soap.base64
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Soap.SoapFormatter
            : and then encoded with base64 encoding.

    mimetype: application/x-microsoft.net.object.bytearray.base64
    value   : The object must be serialized into a byte array 
            : using a System.ComponentModel.TypeConverter
            : and then encoded with base64 encoding.
    -->
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  
  <!-- 애플리케이션 제목 -->
  <data name="AppTitle" xml:space="preserve">
    <value>모자이크 검열 시스템 (스티커 지원)</value>
  </data>
  
  <!-- 그룹 제목들 -->
  <data name="GroupControls" xml:space="preserve">
    <value>시스템 제어</value>
  </data>
  <data name="GroupSettings" xml:space="preserve">
    <value>설정</value>
  </data>
  <data name="GroupTargets" xml:space="preserve">
    <value>검열 대상</value>
  </data>
  <data name="GroupLog" xml:space="preserve">
    <value>로그</value>
  </data>
  
  <!-- 버튼 텍스트들 -->
  <data name="ButtonStart" xml:space="preserve">
    <value>시작</value>
  </data>
  <data name="ButtonStop" xml:space="preserve">
    <value>중지</value>
  </data>
  <data name="ButtonTest" xml:space="preserve">
    <value>캡처 테스트</value>
  </data>
  
  <!-- 레이블 텍스트들 -->
  <data name="LabelFps" xml:space="preserve">
    <value>FPS:</value>
  </data>
  <data name="LabelDetection" xml:space="preserve">
    <value>객체 감지 활성화</value>
  </data>
  <data name="LabelEffect" xml:space="preserve">
    <value>검열 효과 활성화</value>
  </data>
  <data name="LabelCensorTypeMosaic" xml:space="preserve">
    <value>모자이크</value>
  </data>
  <data name="LabelCensorTypeBlur" xml:space="preserve">
    <value>블러</value>
  </data>
  <data name="LabelCensorStrength" xml:space="preserve">
    <value>검열 강도:</value>
  </data>
  <data name="LabelConfidence" xml:space="preserve">
    <value>신뢰도:</value>
  </data>
  <data name="LabelExecutionMode" xml:space="preserve">
    <value>실행 모드:</value>
  </data>
  <data name="LabelStickers" xml:space="preserve">
    <value>스티커 활성화</value>
  </data>
  
  <!-- 검열 대상 번역 -->
  <data name="Target_얼굴" xml:space="preserve">
    <value>얼굴</value>
  </data>
  <data name="Target_가슴" xml:space="preserve">
    <value>가슴</value>
  </data>
  <data name="Target_겨드랑이" xml:space="preserve">
    <value>겨드랑이</value>
  </data>
  <data name="Target_보지" xml:space="preserve">
    <value>보지</value>
  </data>
  <data name="Target_발" xml:space="preserve">
    <value>발</value>
  </data>
  <data name="Target_몸 전체" xml:space="preserve">
    <value>몸 전체</value>
  </data>
  <data name="Target_자지" xml:space="preserve">
    <value>자지</value>
  </data>
  <data name="Target_팬티" xml:space="preserve">
    <value>팬티</value>
  </data>
  <data name="Target_눈" xml:space="preserve">
    <value>눈</value>
  </data>
  <data name="Target_손" xml:space="preserve">
    <value>손</value>
  </data>
  <data name="Target_교미" xml:space="preserve">
    <value>교미</value>
  </data>
  <data name="Target_신발" xml:space="preserve">
    <value>신발</value>
  </data>
  <data name="Target_가슴_옷" xml:space="preserve">
    <value>가슴_옷</value>
  </data>
  <data name="Target_여성" xml:space="preserve">
    <value>여성</value>
  </data>
  
  <!-- GPU 상태 번역 -->
  <data name="GPU_CUDA" xml:space="preserve">
    <value>CUDA (NVIDIA GPU)</value>
  </data>
  <data name="GPU_DirectML" xml:space="preserve">
    <value>DirectML (Windows GPU)</value>
  </data>
  <data name="GPU_CPU" xml:space="preserve">
    <value>CPU (소프트웨어)</value>
  </data>
  
  <!-- 상태 메시지들 -->
  <data name="StatusReady" xml:space="preserve">
    <value>⭕ 시스템 대기 중</value>
  </data>
  <data name="StatusRunning" xml:space="preserve">
    <value>🚀 시스템 실행 중...</value>
  </data>
</root>