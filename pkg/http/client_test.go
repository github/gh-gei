package http

import (
	"context"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestNewClient(t *testing.T) {
	log := logger.New(false)
	cfg := DefaultConfig()

	client := NewClient(cfg, log)

	assert.NotNil(t, client)
	assert.NotNil(t, client.httpClient)
	assert.NotNil(t, client.retryPolicy)
	assert.NotNil(t, client.logger)
}

func TestClient_Get(t *testing.T) {
	log := logger.New(false)

	t.Run("successful GET request", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, http.MethodGet, r.Method)
			assert.Equal(t, "Bearer test-token", r.Header.Get("Authorization"))
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"message":"success"}`))
		}))
		defer server.Close()

		client := NewClient(DefaultConfig(), log)
		ctx := context.Background()

		headers := map[string]string{
			"Authorization": "Bearer test-token",
		}

		body, err := client.Get(ctx, server.URL, headers)

		require.NoError(t, err)
		assert.Equal(t, `{"message":"success"}`, string(body))
	})

	t.Run("GET request with retry on 500", func(t *testing.T) {
		attempts := 0
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			attempts++
			if attempts < 2 {
				w.WriteHeader(http.StatusInternalServerError)
				return
			}
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("success"))
		}))
		defer server.Close()

		cfg := DefaultConfig()
		cfg.RetryAttempts = 3
		client := NewClient(cfg, log)
		ctx := context.Background()

		body, err := client.Get(ctx, server.URL, nil)

		require.NoError(t, err)
		assert.Equal(t, "success", string(body))
		assert.Equal(t, 2, attempts)
	})

	t.Run("GET request fails after max retries", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusInternalServerError)
			w.Write([]byte("server error"))
		}))
		defer server.Close()

		cfg := DefaultConfig()
		cfg.RetryAttempts = 2
		client := NewClient(cfg, log)
		ctx := context.Background()

		_, err := client.Get(ctx, server.URL, nil)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "HTTP 500")
	})
}

func TestClient_Post(t *testing.T) {
	log := logger.New(false)

	t.Run("successful POST request", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, http.MethodPost, r.Method)
			assert.Equal(t, "application/json", r.Header.Get("Content-Type"))
			w.WriteHeader(http.StatusCreated)
			w.Write([]byte(`{"id":"123"}`))
		}))
		defer server.Close()

		client := NewClient(DefaultConfig(), log)
		ctx := context.Background()

		body, err := client.Post(ctx, server.URL, []byte(`{"name":"test"}`), nil)

		require.NoError(t, err)
		assert.Equal(t, `{"id":"123"}`, string(body))
	})
}

func TestClient_PostJSON(t *testing.T) {
	log := logger.New(false)

	t.Run("successful POST JSON request", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, http.MethodPost, r.Method)
			assert.Equal(t, "application/json", r.Header.Get("Content-Type"))
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"result":"ok"}`))
		}))
		defer server.Close()

		client := NewClient(DefaultConfig(), log)
		ctx := context.Background()

		payload := map[string]string{"name": "test"}
		body, err := client.PostJSON(ctx, server.URL, payload, nil)

		require.NoError(t, err)
		assert.Equal(t, `{"result":"ok"}`, string(body))
	})
}

func TestClient_Put(t *testing.T) {
	log := logger.New(false)

	t.Run("successful PUT request", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, http.MethodPut, r.Method)
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"updated":true}`))
		}))
		defer server.Close()

		client := NewClient(DefaultConfig(), log)
		ctx := context.Background()

		body, err := client.Put(ctx, server.URL, []byte(`{"field":"value"}`), nil)

		require.NoError(t, err)
		assert.Equal(t, `{"updated":true}`, string(body))
	})
}

func TestClient_Delete(t *testing.T) {
	log := logger.New(false)

	t.Run("successful DELETE request", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, http.MethodDelete, r.Method)
			w.WriteHeader(http.StatusNoContent)
		}))
		defer server.Close()

		client := NewClient(DefaultConfig(), log)
		ctx := context.Background()

		err := client.Delete(ctx, server.URL, nil)

		require.NoError(t, err)
	})
}

func TestClient_NoSSLVerify(t *testing.T) {
	log := logger.New(false)

	cfg := DefaultConfig()
	cfg.NoSSLVerify = true

	client := NewClient(cfg, log)

	assert.NotNil(t, client)
	// Cannot easily test SSL verification without setting up an HTTPS server
	// But we verify the client is created successfully
}

func TestClient_Timeout(t *testing.T) {
	log := logger.New(false)

	t.Run("request times out", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			time.Sleep(2 * time.Second)
			w.WriteHeader(http.StatusOK)
		}))
		defer server.Close()

		cfg := DefaultConfig()
		cfg.Timeout = 100 * time.Millisecond
		cfg.RetryAttempts = 1
		client := NewClient(cfg, log)
		ctx := context.Background()

		_, err := client.Get(ctx, server.URL, nil)

		require.Error(t, err)
	})
}
