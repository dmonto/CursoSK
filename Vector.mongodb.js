use('CursoSK');

db.createCollection('Embeddings');

db.getCollection('Embeddings').createSearchIndex(
  'vector_index',
  {
    mappings: {
      dynamic: false,
      fields: {
        Vector: {
          type: "vector",
          numDimensions: 3072,
          similarity: "cosine"
        }
      }
    }
  }
);
