package azure

import (
	"context"
	"fmt"
	"io"
	"strings"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/sas"
	"github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/service"
)

const defaultBlockSizeSDK = 4 * 1024 * 1024 // 4 MB

// azureBlobService is the real BlobService backed by the Azure SDK.
type azureBlobService struct {
	client    *azblob.Client
	sharedKey *service.SharedKeyCredential
}

// newAzureBlobService creates a BlobService from an Azure storage connection string.
func newAzureBlobService(connectionString string) (*azureBlobService, error) {
	client, err := azblob.NewClientFromConnectionString(connectionString, nil)
	if err != nil {
		return nil, fmt.Errorf("creating azure blob client from connection string: %w", err)
	}

	// Parse connection string to extract AccountName and AccountKey for SAS generation
	accountName, accountKey, err := parseConnectionString(connectionString)
	if err != nil {
		return nil, fmt.Errorf("parsing connection string for SAS credentials: %w", err)
	}

	sharedKey, err := service.NewSharedKeyCredential(accountName, accountKey)
	if err != nil {
		return nil, fmt.Errorf("creating shared key credential: %w", err)
	}

	return &azureBlobService{
		client:    client,
		sharedKey: sharedKey,
	}, nil
}

// parseConnectionString extracts AccountName and AccountKey from a standard Azure storage connection string.
func parseConnectionString(connStr string) (accountName, accountKey string, err error) {
	parts := strings.Split(connStr, ";")
	for _, part := range parts {
		part = strings.TrimSpace(part)
		if strings.HasPrefix(part, "AccountName=") {
			accountName = strings.TrimPrefix(part, "AccountName=")
		} else if strings.HasPrefix(part, "AccountKey=") {
			accountKey = strings.TrimPrefix(part, "AccountKey=")
		}
	}
	if accountName == "" || accountKey == "" {
		return "", "", fmt.Errorf("connection string must contain AccountName and AccountKey")
	}
	return accountName, accountKey, nil
}

func (s *azureBlobService) CreateContainer(ctx context.Context, name string) error {
	_, err := s.client.CreateContainer(ctx, name, nil)
	if err != nil {
		return fmt.Errorf("creating blob container %q: %w", name, err)
	}
	return nil
}

func (s *azureBlobService) UploadBlob(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error {
	opts := &azblob.UploadStreamOptions{
		BlockSize: defaultBlockSizeSDK,
	}

	_, err := s.client.UploadStream(ctx, container, blob, content, opts)
	if err != nil {
		return fmt.Errorf("uploading blob %q to container %q: %w", blob, container, err)
	}

	// Report final progress
	if progressFn != nil {
		progressFn(size)
	}

	return nil
}

func (s *azureBlobService) GenerateSASURL(container, blob string, expiry time.Duration) (string, error) {
	now := time.Now().UTC()
	sasQueryParams, err := sas.BlobSignatureValues{
		Protocol:      sas.ProtocolHTTPS,
		StartTime:     now,
		ExpiryTime:    now.Add(expiry),
		Permissions:   (&sas.BlobPermissions{Read: true}).String(),
		ContainerName: container,
		BlobName:      blob,
	}.SignWithSharedKey(s.sharedKey)
	if err != nil {
		return "", fmt.Errorf("generating SAS for blob %q in container %q: %w", blob, container, err)
	}

	blobURL := s.client.URL() + container + "/" + blob + "?" + sasQueryParams.Encode()
	return blobURL, nil
}
